namespace YALuaToy.Core {

using System;

using System.Collections.Generic;
using YALuaToy.Const;
using YALuaToy.Debug;

using InstructionIndex = System.Int32; /* 指令索引 */

/// <summary>所有权在 LuaProto</summary>
internal struct UpvalueDesc
{
    private LuaString _name;
    private bool      _instack;
    private int       _ldx; /* 当 instack=true 时为 absldx（栈帧索引绝对值，从 1 开始），当 instack=false 时为上值索引（从 1 开始） */

    public string Name    => _name != null ? _name.Str : "(*no name)";
    public bool   InStack => _instack; /* 注意这个 instack 是编译期可知的，表示 proto 使用的上值是否在栈上。
                                          但是！就算 instack == false，也不意味着从上值列表取出的 Upvalue 就不在栈上了。
                                          Upvalue 一开始永远在栈上，在运行时关闭，这是编译期不可知的。要把这两个概念区分开来 */
    public int    Ldx     => _ldx;

    /* 构造时注意一下，CLua 中 _ldx 对应的字段是 idx，它在 instack==false 时是从 0 开始的；
       重点关注一下 `uv[i].idx` 这种，或者直接搜 Upvaldesc，看 CLua 全部使用案例 */
    public UpvalueDesc(string name, bool instack, int ldx): this(new LuaString(name), instack, ldx) { }
    public UpvalueDesc(LuaString name, bool instack, int ldx) {
        _name    = name;
        _instack = instack;
        _ldx     = ldx;
    }

    public override string ToString() {
        return $"<uvd name: {_name}, instack: {_instack}, ldx: {_ldx}>";
    }
}

/// <summary>所有权在 LuaProto</summary>
internal class LocalVar
{
    internal LuaString varName;
    internal int       startPC; /* 当 pc <= startPC 时，变量有效 */
    internal int       endPC;   /* 当 pc >= endPC 时，变量无效 */

    public LocalVar(LuaString name) {
        varName = name;
        startPC = endPC = 0;
    }

    public override string ToString() {
        return $"<lv varname: {varName}, startpc: {startPC}, endpc: {endPC}>";
    }
}

/* frameSize（即 CLua 的 maxstacksize）应该是不包含存放函数的那个槽的，而是从第一个固定参数位置开始算，具体看 _AdjustVarargs */
/* LuaProto 内几个 List 理论上都能换成原生数组，因为编译后就固定长度了，但懒得搞太复杂了 */
internal class LuaProto : LuaObject
{
    /* ---------------- Members ---------------- */

    internal byte          _paramCount;          /* 固定参数数量 */
    internal bool          _vararg;              /* 是否有可变参数 */
    internal byte          _frameSize;           /* 该原型需要的栈空间（寄存器数） */
    private List<LuaValue> _constants;           /* 常量表，先初始化为 null，如果发现真的有常量再创建 */
    internal List<Instruction> _instructions;    /* 指令表，这里 CLua 初始化为 null，但我还是初始化，感觉是必要的 */
    private List<LuaProto>     _subProtos;       /* 子原型列表，先初始化为 null，如果发现真的有子函数再创建 */
    private List<LocalVar>     _localVars;       /* 局部变量表，注意可能有无效变量在里面；这里 CLua 初始化为 null，但我还是初始化了 */
    private List<UpvalueDesc>  _upvalueDescList; /* 上值列表，先初始化为 null，如果发现真的有上值再创建 */
    private LClosure           _lclosureCache;   /* 闭包缓存 */
    /* debug */
    internal int                _firstLine; /* 原型代码定义所在行 */
    internal int                _lastLine;  /* 原型代码最后一行 */
    private List<int>           _lines;     /* 指令到源码的映射，这里 CLua 初始化为 null，但我还是初始化，感觉是必要的 */
    internal readonly LuaString source;

    public byte  ParamCount                  => _paramCount;
    public bool  Vararg                      => _vararg;
    public byte  FrameSize                   => _frameSize;
    internal int UpvalueCount                => _upvalueDescList == null ? 0 : _upvalueDescList.Count;
    internal List<LuaValue> Constants        => _constants;
    internal List<LuaProto> SubProtos        => _subProtos;
    internal List<LocalVar>  LocalVars       => _localVars;
    public List<UpvalueDesc> UpvalueDescList => _upvalueDescList;
    public List<int>         Lines           => _lines;
    internal LClosure        LClosureCache {
        get => _lclosureCache;
        set => _lclosureCache = value;
    }

    public LuaProto(LuaString source) {
        _type            = LuaConst.MarkLuaObject(LuaConst.TPROTO);
        _constants       = null;
        _subProtos       = null;
        _instructions    = new List<Instruction>();
        _lclosureCache   = null;
        _lines           = new List<int>();
        _upvalueDescList = null;
        _paramCount      = 0;
        _vararg          = false;
        _frameSize       = 0;
        _localVars       = null;
        _firstLine       = 0;
        _lastLine        = 0;
        this.source      = source;
    }

    /* parser 时专用的接口，多一个 if 判断以自动初始化 */
    internal List<LuaValue> GetConstants() {
        if (_constants == null)
            _constants = new List<LuaValue>();
        return _constants;
    }
    internal List<LuaProto> GetSubProtos() {
        if (_subProtos == null)
            _subProtos = new List<LuaProto>();
        return _subProtos;
    }
    internal List<LocalVar> GetLocalVars() {
        if (_localVars == null)
            _localVars = new List<LocalVar>();
        return _localVars;
    }
    internal List<UpvalueDesc> GetUpvalueDescList() {
        if (_upvalueDescList == null)
            _upvalueDescList = new List<UpvalueDesc>();
        return _upvalueDescList;
    }
    internal int ConstantsCount   => _constants == null ? 0 : _constants.Count;
    internal int SubProtosCount   => _subProtos == null ? 0 : _subProtos.Count;
    internal int LocalVarsCount   => _localVars == null ? 0 : _localVars.Count;
    internal int UpvalueDescCount => _upvalueDescList == null ? 0 : _upvalueDescList.Count;

    /* ---------------- API ---------------- */

    public Instruction GetInstruction(InstructionIndex idx) {
        return _instructions[idx];
    }

    /* 调试用途，这里的 localVarIdx 是指 _localVars 的索引，排除无效变量 */
    public string GetLocalVarName(int localVarIdx, int pc) {
        for (int i = 0; i < _localVars.Count && _localVars[i].startPC <= pc; i++) {
            if (pc < _localVars[i].endPC) { /* 跳过无效变量 */
                localVarIdx--;
                if (localVarIdx == 0)
                    return _localVars[i].varName.Str;
            }
        }
        return null;
    }

    /* ---------------- Utils ---------------- */

    public override string ToString() {
        return $"<pro source: {source}, line: {_firstLine}-{_lastLine}, paramCount: {_paramCount}, vararg: {_vararg}, frame: {_frameSize}>";
    }
}

/* 所有权为 LClosure */
internal class Upvalue
{
    /* 注意字段 _next 的名字虽然叫 _next，但其实按创建顺序来说是倒序，不过上值本来就不关注顺序，无伤大雅 */
    private bool     _open;   /* 是否仍处于打开状态（即 _val 是否还在栈上） */
    private RawIdx   _rawIdx; /* 栈索引，仅当 _open == true 时，该值有意义 */
    private Upvalue  _next;   /* 上值链表，记录一个闭包内的所有上值 */
    private LuaValue _val;    /* 仅当 _open == false 时，该值有意义 */
    private LuaState _thread;

    public bool    Open   => _open;
    public RawIdx  RawIdx => _rawIdx;
    public Upvalue Next {
        get => _next;
        set => _next = value;
    }
    public LuaState Thread => _thread;

    public Upvalue(in LuaValue value, Upvalue next = null): this(false, next) {
        _val = value;
    }
    public Upvalue(LuaState thread, RawIdx rawIdx, Upvalue next = null): this(true, next, thread) {
        _rawIdx = rawIdx;
    }
    public Upvalue(bool open = false, Upvalue next = null, LuaState thread = null) {
        _rawIdx = (RawIdx)(-1);
        _val    = LuaValue.NIL;
        _open   = open;
        _next   = next;
        _thread = thread;
    }

    internal void _Reset(LuaState thread, RawIdx rawIdx) {
        _rawIdx = rawIdx;
        _val    = LuaValue.NIL;
        _open   = true;
        _thread = thread;
    }
    internal void _Reset(in LuaValue value) {
        _rawIdx = (RawIdx)(-1);
        _val    = value;
        _open   = false;
        _thread = null;
    }

    public override string ToString() {
        return $"<up open: {_open}, rawidx: {_rawIdx}, val: {_val}>";
    }

    internal LuaValue _GetValue() {
        LuaDebug.Assert(!_open || _thread != null, "Cannot get open upvalue with empty thread.");
        return _open && _thread != null ? _thread.GetStack(_rawIdx) : _val;
    }
    internal void _Close(in LuaValue stackElem) {
        LuaDebug.Check(_open, "Cannot close open upvalue.");
        _val    = stackElem;
        _rawIdx = (RawIdx)(-1);
        _next   = null;
        _thread = null;
        _open   = false;
    }
}

internal abstract class Closure : LuaObject
{
    public bool              IsLClosure => LuaType.CheckVariant(_type, LuaConst.TLCL);
    public abstract bool     IsValidUpvalueLdx(int ldx);
    public abstract LuaValue GetUpvalue(int upvalueLdx);
    public abstract int      UpvalueCount { get; }
}

internal class CClosure : Closure
{
    public readonly LuaCFunction func;
    private readonly             LuaValue[] _upvalues;

    public override int UpvalueCount => _upvalues.Length;

    public CClosure(LuaCFunction func, int upvalueCount) {
        _type     = LuaConst.MarkLuaObject(LuaConst.TCCL);
        _upvalues = new LuaValue[upvalueCount];
        this.func = func;
    }

    public override bool IsValidUpvalueLdx(int upvalueLdx) {
        return 1 <= upvalueLdx && upvalueLdx <= _upvalues.Length;
    }
    public override LuaValue GetUpvalue(int upvalueLdx) {
        return _upvalues[upvalueLdx - 1];
    }
    public void SetUpvalue(int upvalueLdx, in LuaValue upvalue) {
        _upvalues[upvalueLdx - 1] = upvalue;
    }

    public override string ToString() {
        return $"<cc func: {func}>";
    }
}

/* 该结构可由 vm、parser、undumper 创建 */
internal class LClosure : Closure
{
    public readonly LuaProto proto;
    private readonly         Upvalue[] _upvalues; /* 初始化为 null 即可 */

    public override int UpvalueCount => _upvalues.Length;

    public LClosure(LuaProto proto, int upvalueCount) {
        _type      = LuaConst.MarkLuaObject(LuaConst.TLCL);
        _upvalues  = new Upvalue[upvalueCount];
        this.proto = proto;
    }

    /* 仅个别情况需要用到这个来初始化 */
    internal void _InitAllUpvalues() {
        for (int i = 0; i < _upvalues.Length; i++)
            _upvalues[i] = new Upvalue();
    }

    public override bool IsValidUpvalueLdx(int upvalueLdx) {
        return 1 <= upvalueLdx && upvalueLdx <= _upvalues.Length;
    }
    public override LuaValue GetUpvalue(int upvalueLdx) {
        return _upvalues[upvalueLdx - 1]._GetValue();
    }
    public void SetUpvalue(LuaState thread, int upvalueLdx, RawIdx rawIdx) {
        /* 设置打开的上值（注意不能替换 _upvalues 的元素，只能【原地】修改） */
        _upvalues[upvalueLdx - 1]._Reset(thread, rawIdx);
    }
    public void SetUpvalue(int upvalueLdx, in LuaValue value) {
        /* 设置关闭的上值（注意不能替换 _upvalues 的元素，只能【原地】修改） */
        _upvalues[upvalueLdx - 1]._Reset(value);
    }

    internal Upvalue GetUpvalueObj(int upvalueLdx) {
        return _upvalues[upvalueLdx - 1];
    }
    internal void SetUpvalueObj(int upvalueLdx, Upvalue upvalue) {
        _upvalues[upvalueLdx - 1] = upvalue;
    }

    public override string ToString() {
        return $"<lc proto: {proto}>";
    }
}

public partial class LuaState
{
    internal bool InTwups => _twups != this;

    internal void CloseUpvalues(RawIdx to) {
        while (_openUpVals != null && _openUpVals.RawIdx >= to) {
            LuaDebug.Check(_openUpVals.Open, "Close value shouldn't be on openUpVals.");
            Upvalue next = _openUpVals.Next;
            _openUpVals._Close(_stack[(int)_openUpVals.RawIdx]);
            _openUpVals = next;
        }
    }

    /* 查找指向指定栈元素但上值，找不到就创建 */
    internal Upvalue FindewUpvalue(RawIdx target) {
        LuaDebug.Check(InTwups || _openUpVals == null, "There should not be any open upvalues when in twups.");
        Upvalue openUpVals = _openUpVals;
        Upvalue prevUpVals = null;
        while (openUpVals != null && openUpVals.RawIdx >= target) {
            LuaDebug.Check(openUpVals.Open, "Close value shouldn't be on openUpVals.");
            if (openUpVals.RawIdx == target)
                return openUpVals;
            prevUpVals = openUpVals;
            openUpVals = openUpVals.Next;
        }

        Upvalue newUpvalue = new Upvalue(this, target, next: openUpVals);
        if (prevUpVals == null) /* 插入 openUpVals 链表 */
            _openUpVals = newUpvalue;
        else
            prevUpVals.Next = newUpvalue;
        if (!InTwups) { /* 插入 twups 链表 */
            _twups             = globalState._twups;
            globalState._twups = this;
        }
        return newUpvalue;
    }
}

}
