namespace YALuaToy.Compilation {

using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Antlr4.Runtime;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Core;
using YALuaToy.Compilation.Antlr;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using FrameIndex       = System.Byte;  /* 栈帧索引（0 表示栈帧起点，寄存器应该是从 1 开始的） */
using InstructionIndex = System.Int32; /* 指令索引 */

internal enum ExpType {
    VOID,      /* 当 expdesc 正在表达“列表的最后一项表达式”时，该类型的含义是空列表（没有表达式） */
    NIL,       /* constant nil */
    TRUE,      /* constant true */
    FALSE,     /* constant false */
    K,         /* 常量索引【miscInfo 含义：常量索引】 */
    FLT,       /* 浮点数常量 */
    INT,       /* 整型常量 */
    NONRELOC,  /* 值在固定寄存器上（通常就是 freereg-1 那个位置）的表达式，【miscInfo 含义：寄存器】 */
    LOCAL,     /* 局部变量【miscInfo 含义：寄存器 FrameIndex】 */
    UPVAL,     /* 上值变量【miscInfo 含义：upvalues 上值索引】 */
    INDEXED,   /* 索引变量（a[b] 形式）
                   ind.vt = whether 't' is register or upvalue;
                   ind.t = table register or upvalue;
                   ind.idx = key's R/K index */
    JMP,       /* VJMP 代表哪些表达式？条件跳转，【miscInfo 含义：跳转指令 pc 索引】 */
    RELOCABLE, /* 表达式的值可以放在寄存器上【miscInfo 含义：待回填寄存器 pc】 */
    CALL,      /* 函数调用表达式【miscInfo 含义：指令 pc 索引】 */
    VARARG     /* 可变参数（...）【miscInfo 含义：OP_VARARG 指令的 pc 索引】 */
}
internal static class ExpTypeExtensions
{
    /* 该 expkind 的值是否是变量（否则就是表达式） */
    public static bool IsVar(this ExpType type) {
        return type == ExpType.LOCAL || type == ExpType.UPVAL || type == ExpType.INDEXED;
    }
    /* 该 expkind 的值是否在当前栈帧的寄存器上 */
    public static bool IsInReg(this ExpType type) {
        return type == ExpType.NONRELOC || type == ExpType.LOCAL;
    }
    public static bool HasMultiResults(this ExpType type) {
        return type == ExpType.CALL || type == ExpType.VARARG;
    }
}

internal class ExpDesc
{
    internal static ExpDesc zero = new ExpDesc(0);

    public struct IndexInfo
    {
        public ArgValue key;
        public byte     table; /* 这里可能是 FrameIndex，也可能是上值索引（从 0 开始） */
        public ExpType  tableType;

        public IndexInfo(byte table, ExpType tableType, ArgValue key) {
            this.key       = key;
            this.table     = table;
            this.tableType = tableType;
        }

        public override string ToString() {
            return $"<key: {key}, table: {table}, tabletype: {tableType}>";
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InfoUnion
    {
        [FieldOffset(0)]
        public long ivalue; /* VKINT 的值；for VKINT */
        [FieldOffset(0)]
        public double nvalue; /* VKFLT 的值；for VKFLT */
        [FieldOffset(0)]
        public int miscInfo; /* 通用数据，具体看 ExpType 里的注释（我的建议是，既然都是 Union 了，那就拆的更细一点） */
        [FieldOffset(0)]
        public IndexInfo indexInfo; /* 索引变量（VINDEXED）的描述数据 */
    }

    /* ---------------- Members ---------------- */

    private ExpType           _expType;
    private InfoUnion         _uinfo;
    internal InstructionIndex trueList;  /* true 跳转指令列表 */
    internal InstructionIndex falseList; /* false 跳转指令列表 */

    public ExpDesc(): this(ExpType.VOID) { }
    public ExpDesc(ExpType expType) {
        _expType        = expType;
        _uinfo.miscInfo = 0;
        trueList = falseList = FuncState.NO_JUMP;
    }
    public ExpDesc(int i): this((long)i) { }
    public ExpDesc(long i): this(ExpType.INT) {
        _uinfo.ivalue = i;
    }
    public ExpDesc(double n): this(ExpType.FLT) {
        _uinfo.nvalue = n;
    }
    public ExpDesc(ExpType expType, int miscInfo): this(expType) {
        _uinfo.miscInfo = miscInfo;
    }
    public ExpDesc(IndexInfo indexInfo): this(ExpType.INDEXED) {
        _uinfo.indexInfo = indexInfo;
    }

    public override string ToString() {
        return $"<{_expType} - {_GetValueString()}, t: {trueList}, f: {falseList}>";
    }
    internal string _GetValueString() {
        switch (_expType) {
        case ExpType.VOID:
            return "''";
        case ExpType.NIL:
            return "nil";
        case ExpType.TRUE:
            return "true";
        case ExpType.FALSE:
            return "false";
        case ExpType.K:
            return $"k{ConstantsIndex}";
        case ExpType.FLT:
            return Float.ToString();
        case ExpType.INT:
            return Int.ToString();
        case ExpType.NONRELOC:
        case ExpType.LOCAL:
            return $"r{Reg}";
        case ExpType.UPVAL:
            return $"u{UpvalueIndex}";
        case ExpType.INDEXED:
            return IndexInfo_.ToString();
        case ExpType.JMP:
        case ExpType.RELOCABLE:
        case ExpType.CALL:
        case ExpType.VARARG:
            return $"i{InstIndex}";
        }
        return "unknown";
    }

    public bool    HasJump => trueList != falseList; /* 是否有跳转 */
    public ExpType Type    => _expType;

    public bool Bool {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.TRUE, ExpType.FALSE);
            return _expType == ExpType.TRUE;
        }
    }
    public long Int {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.INT);
            return _uinfo.ivalue;
        }
    }
    public double Float {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.FLT);
            return _uinfo.nvalue;
        }
    }
    public IndexInfo IndexInfo_ {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.INDEXED);
            return _uinfo.indexInfo;
        }
    }
    public ushort ConstantsIndex {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.K);
            return (ushort)_uinfo.miscInfo;
        }
    }
    public FrameIndex Reg {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.NONRELOC, ExpType.LOCAL);
            return (FrameIndex)_uinfo.miscInfo;
        }
    }
    public ushort UpvalueIndex {
        get {
            LuaDebug.AssertExpType(_expType, ExpType.UPVAL);
            return (ushort)_uinfo.miscInfo;
        }
    }
    public InstructionIndex InstIndex {
        get {
            /* 需要回填指令的类型 */
            LuaDebug.AssertExpType(_expType, ExpType.JMP, ExpType.CALL, ExpType.VARARG, ExpType.RELOCABLE);
            return _uinfo.miscInfo;
        }
    }

    public void Reset(bool resetJump = false) {
        _Reset(ExpType.VOID, resetJump);
    }
    public void Reset(ExpType expType, bool resetJump = false) {
        LuaDebug.AssertExpType(expType, ExpType.VOID, ExpType.NIL, ExpType.TRUE, ExpType.FALSE, ExpType.FLT, ExpType.INT);
        _Reset(expType, resetJump);
    }
    public void Reset(long i, bool resetJump = false) {
        _Reset(ExpType.INT, resetJump);
        _uinfo.ivalue = i;
    }
    public void Reset(double n, bool resetJump = false) {
        _Reset(ExpType.FLT, resetJump);
        _uinfo.nvalue = n;
    }
    public void Reset(ExpType expType, int miscInfo, bool resetJump = false) {
        _Reset(expType, resetJump);
        _uinfo.miscInfo = miscInfo;
    }
    public void Reset(IndexInfo indexInfo, bool resetJump = false) {
        _Reset(ExpType.INDEXED, resetJump);
        _uinfo.indexInfo = indexInfo;
    }
    public void Reset(FuncState funcState, LuaString luaString, bool resetJump = false) {
        _Reset(ExpType.K, resetJump);
        _uinfo.miscInfo = funcState.AddConstant(luaString);
    }
    private void _Reset(ExpType expType, bool resetJump = false) {
        _expType        = expType;
        _uinfo.miscInfo = 0;
        if (resetJump)
            trueList = falseList = FuncState.NO_JUMP;
    }

    public void CopyFrom(ExpDesc target) {
        _expType  = target._expType;
        _uinfo    = target._uinfo;
        trueList  = target.trueList;
        falseList = target.falseList;
    }

    public bool TryGetNumber(out LuaValue value) {
        value = LuaValue.NIL;
        if (HasJump)
            return false; /* cannot change 'e' if it has jumps */
        if (_expType == ExpType.VOID)
            return false; /* cannot change 'e' if it is not a number */
        switch (_expType) {
        case ExpType.INT:
            value = new LuaValue(_uinfo.ivalue);
            return true;
        case ExpType.FLT:
            value = new LuaValue(_uinfo.nvalue);
            return true;
        }
        return false;
    }
}

/* label 和 goto 都用这个结构 */
internal struct LabelDesc
{
    internal LuaString        name;          /* 标签名 */
    internal int              line;          /* 行号 */
    internal InstructionIndex instIndex;     /* 标签位置
                                                如果是 goto 标签，则 pc 表示跳转列表，因为 goto 就是一个无条件跳转；
                                                如果是 label 标签，则 pc 是下个指令地址 */
    internal FrameIndex activeLocalVarCount; /* 标签创建时，活跃的局部局部变量数（注意数量可以当帧索引）；
                                                在回填 pending goto 时用于检测跳转是否合法，确保 goto 不会跳入一个局部变量尚未激活的作用域 */

    public LabelDesc(LuaString name, int line, InstructionIndex instIndex, FrameIndex activeLocalVarCount) {
        this.name                = name;
        this.line                = line;
        this.instIndex           = instIndex;
        this.activeLocalVarCount = activeLocalVarCount;
    }
    public override string ToString() {
        return $"<label: {name} line: {line}, pc: {instIndex}, active: {activeLocalVarCount}>";
    }
}

internal class DynamicData
{
    /* activeLocalVarList 的索引是【活跃变量编号】，元素是 proto.LocalVars 的对应索引 */
    internal List<int> activeVarList;         /* 活跃局部变量列表 */
    internal List<LabelDesc> pendingGotoList; /* 待回填的 goto 语句列表 */
    internal List<LabelDesc> labelList;       /* label 语句列表 */

    public DynamicData() {
        activeVarList   = new List<int>();
        pendingGotoList = new List<LabelDesc>();
        labelList       = new List<LabelDesc>();
    }
}

/* 关于块链的模型，要说明一下。快链是按代码块层级连接的，一个块链上通常【含有多个栈帧】，
   - 举个例子：[mainFuncBlock] -> [func1Block] -> [closureBlock -> whileBlock -> ifBlock]
     这里每个节点都是一个 BlockControl，而中括号括起来的则是一个函数栈帧，最开始为文件函数。FuncState._prev 则是栈帧的链表
*/
internal class BlockControl
{
    internal BlockControl prev;
    internal int          firstGotoIdx;      /* 列表是 DynamicData._gotoList */
    internal int          firstLabelIdx;     /* 列表是 DynamicData._labelList */
    internal FrameIndex   activeOutVarCount; /* 在当前栈帧内，块外的活跃局部变量数，栈帧第一个块该值为 0 */
    internal bool         hasUpvalue;        /* 当前块【目前】是否有局部变量是上值 */
    internal bool         loop;              /* 当前块是否为循环体 */

    public BlockControl(BlockControl prev, int firstGotoIdx, int firstLabelIdx, FrameIndex activeOutVarCount, bool hasUpvalue, bool loop):
        this(firstGotoIdx, firstLabelIdx, activeOutVarCount, hasUpvalue, loop)  //
    {
        this.prev = prev;
    }
    public BlockControl(int firstGotoIdx, int firstLabelIdx, FrameIndex activeOutVarCount, bool hasUpvalue, bool loop) {
        this.prev              = null;
        this.firstGotoIdx      = firstGotoIdx;
        this.firstLabelIdx     = firstLabelIdx;
        this.activeOutVarCount = activeOutVarCount;
        this.hasUpvalue        = hasUpvalue;
        this.loop              = loop;
    }
}

internal partial class FuncState
{
    private LuaProto          _proto;         /* 当前函数的原型 */
    private FuncState         _prev;          /* 外层函数状态；enclosing function */
    private LuaCodeTranslator _translator;    /* 字节码翻译器，parser 到 code 的中间逻辑 */
    private LuaTable          _constantsIMap; /* 常量索引映射【缓存】表，key 是常量值，value 是常量表索引 */
    private LuaState          _state;
    internal BlockControl     block;      /* 块链表（层级链）；chain of current blocks */
    private InstructionIndex  _pc;        /* 下个代码的位置；next position to code (equivalent to 'ncode') */
    internal InstructionIndex lastTarget; /* 上个被设为跳转目标的指令地址。该字段仅用于“连续指令合并”的优化，参考 luaK_getlabel */
    internal InstructionIndex jpc;        /* 目标为当前 pc 的跳转链表（当前指令还未设置，如果当前也是跳转，那会连接） */
    internal int              firstActiveLocalVarIdx; /* 第一个局部变量在 DynamicData.activeVarList 中的索引 */
    internal FrameIndex       activeLocalVarCount;    /* 当前活跃局部变量的数量，可以理解为：当前最新局部变量寄存器的【下个位置】 */
    private FrameIndex        _freeReg;               /* 第一个空闲寄存器 */

    public LuaProto   Proto => _proto;
    public FuncState  Prev  => _prev;
    public FrameIndex FreeReg {
        get => _freeReg;
        set => _freeReg = value;
    }
    public int Pc {
        get => _pc;
        set => _pc = value;
    }
    public LuaCodeTranslator Translator => _translator;

    public FuncState(
        LuaState          state,
        LuaCodeTranslator listener,
        LuaTable          constantsIMap,
        FuncState         prev,
        int               firstActiveLocalVarIdx,
        LuaString         source
    ):
        this(state, listener, constantsIMap, prev, firstActiveLocalVarIdx, source, new LuaProto(source)) { }
    public FuncState(
        LuaState          state,
        LuaCodeTranslator listener,
        LuaTable          constantsIMap,
        FuncState         prev,
        int               firstActiveLocalVarIdx,
        LuaString         source,
        LuaProto          proto
    ) {
        this._state                 = state;
        this._proto                 = proto;
        this._constantsIMap         = constantsIMap;
        this._translator            = listener;
        this._prev                  = prev;
        this.firstActiveLocalVarIdx = firstActiveLocalVarIdx;

        this.block               = null;
        this.Pc                  = 0;
        this.lastTarget          = 0;
        this.jpc                 = NO_JUMP;
        this.activeLocalVarCount = 0;
        this.FreeReg             = 0;
    }
}

internal static class LuaParserUtils
{
    public static ThreadStatus Parse(LuaState state, AntlrInputStream inputStream, string source) {
        int DoParse(LuaState state_) {
            _RawParse(state_, inputStream, source);
            return 1;
        }
        state.PushStack(DoParse);
        ThreadStatus threadStatus = state.PCall(0, 1, 0);
        if (threadStatus == ThreadStatus.OK) { /* 完成 lua_load 上的环境变量初始化 */
            LClosure lclosure = state.GetStack(state.Top - 1).LObject<LClosure>();
            if (lclosure.UpvalueCount >= 1) {
                LuaValue _G = state.globalState.Registry.Get(new LuaValue(LuaConst.RIDX_GLOBALS));
                lclosure.SetUpvalue(1, _G); /* 把 env 设为 _G */
            }
        }
        return threadStatus;
    }
    internal static LClosure _RawParse(LuaState state, AntlrInputStream inputStream, string source) {
        LuaTable constantsIMap = new LuaTable(); /* 新建常量索引映射表 */

        /* Lexer */
        LuaLexer lexer                = new LuaLexer(inputStream, constantsIMap);
        lexer.TokenFactory            = new LTokenFactory();
        CommonTokenStream tokenStream = new CommonTokenStream(lexer);

        /* Parser */
        LuaParser parser = new LuaParser(tokenStream);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new LuaParserErrorListener()); /* 覆盖异常处理 */
        LuaParser.StartContext root = parser.start();

        /* Translator */
        LuaCodeTranslator translator = new LuaCodeTranslator(state, constantsIMap, new LuaString(source));
        LClosure          lclosure   = translator.TranslateStart(root);
        LuaDebug.Assert(lclosure.UpvalueCount == lclosure.proto.UpvalueCount);

        /* Prepare LClosure */
        lclosure._InitAllUpvalues();
        return lclosure;
    }

    internal static void Check(bool condition, string msg) {
        if (!condition)
            throw new LuaSyntaxError(msg);
    }
    internal static void CheckLimit(FuncState funcState, int curr, int limit, string what) {
        if (curr > limit)
            throw new LuaOverLimitError(funcState, limit, what);
    }
}

internal class LuaParserErrorListener : BaseErrorListener
{
    public override void SyntaxError(
        TextWriter           output,
        IRecognizer          recognizer,
        IToken               offendingSymbol,
        int                  line,
        int                  charPositionInLine,
        string               msg,
        RecognitionException e
    ) {
        // 你可以用自定义异常，例如 LuaSyntaxError
        throw new LuaSyntaxError($"line {line}:{charPositionInLine} {msg}");
    }
}

}  // namespace YALuaToy. Compilation
