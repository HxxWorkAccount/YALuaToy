namespace YALuaToy.Core {

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Metadata;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Compilation;
using YALuaToy.Compilation.Antlr;

internal struct RawIdx
{
    public static readonly RawIdx InvalidErrorFunc = new RawIdx(0); /* 0 也可以，栈初始位置是被占用的 */

    private int _rawIdx;

    public RawIdx(int rawIdx) {
        _rawIdx = rawIdx;
    }

    public static explicit operator int(RawIdx rawIdx) {
        return rawIdx._rawIdx;
    }
    public static explicit operator RawIdx(int rawIdx) {
        return new RawIdx(rawIdx);
    }

    public static RawIdx operator +(RawIdx rawIdx, int offset) {
        return new RawIdx(rawIdx._rawIdx + offset);
    }
    public static RawIdx operator -(RawIdx rawIdx, int offset) {
        return new RawIdx(rawIdx._rawIdx - offset);
    }
    public static int operator -(RawIdx rawIdx, RawIdx other) {
        return rawIdx._rawIdx - other._rawIdx;
    }
    public static RawIdx operator ++(RawIdx rawIdx) {
        rawIdx._rawIdx++;
        return rawIdx;
    }
    public static RawIdx operator --(RawIdx rawIdx) {
        rawIdx._rawIdx--;
        return rawIdx;
    }
    public static bool operator ==(RawIdx a, RawIdx b) {
        return a._rawIdx == b._rawIdx;
    }
    public static bool operator !=(RawIdx a, RawIdx b) {
        return a._rawIdx != b._rawIdx;
    }
    public static bool operator<(RawIdx a, RawIdx b) {
        return a._rawIdx < b._rawIdx;
    }
    public static bool operator>(RawIdx a, RawIdx b) {
        return a._rawIdx > b._rawIdx;
    }
    public static bool operator <=(RawIdx a, RawIdx b) {
        return a._rawIdx <= b._rawIdx;
    }
    public static bool operator >=(RawIdx a, RawIdx b) {
        return a._rawIdx >= b._rawIdx;
    }

    public override bool Equals(object obj) {
        return obj is RawIdx && this == (RawIdx)obj;
    }
    public override int GetHashCode() {
        return _rawIdx.GetHashCode();
    }
    public override string ToString() {
        return $"{_rawIdx}ri";
    }
}

internal partial class LuaGlobalState
{
    public static readonly LuaString tmName    = new LuaString("__name");
    public static readonly LuaString env       = new LuaString(LuaConst.ENV);
    public static readonly LuaString emptyName = new LuaString("");
    private static LuaString[] reservedWords;
    private const int reservedWordCount = LuaLexer.reservedWordCount;

    private LuaTable         _registry;
    private LuaCFunction     _panicFunc;
    public readonly LuaState mainThread;
    private LuaString[] _tagMethodNames;
    private LuaTable[] _metaTables; /* 基元类型的元表 */
    private LuaString _memerrMsg;   /* 内存分配错误时的错误对象，因为内存错误后无法再分配，所以提前分配好 */
    internal LuaState _twups;       /* twups 链表的头节点 */

    public LuaCFunction PanicFunc         => _panicFunc;
    public LuaString[] TagMethodNames     => _tagMethodNames;
    public LuaTable[] MetaTables          => _metaTables;
    public LuaString MemerrMsg            => _memerrMsg;
    public LuaTable  Registry             => _registry;
    public LuaLogger Logger { get; set; }  = null;

    public LuaGlobalState(LuaState mainThread) {
        /* 默认构造器做 lua_newstate 里的基本初始化，不涉及太多内存分配，还有后续初始化 */
        this.mainThread = mainThread;
        _registry       = null;
        _panicFunc      = null;
        _twups          = null;
        _tagMethodNames = null;
        _metaTables     = new LuaTable[LuaConst.TOTALTAGS];
        _memerrMsg      = null;
    }
    internal void _Init() {
        /* init_registry */
        _registry = new LuaTable();
        _registry.Set(new LuaValue(LuaConst.RIDX_MAINTHREAD), new LuaValue(mainThread));  /* 主线程 */
        _registry.Set(new LuaValue(LuaConst.RIDX_GLOBALS), new LuaValue(new LuaTable())); /* 全局表 */

        /* luaT_init */
        _tagMethodNames = new LuaString[LuaConst.TAG_METHOD_NAMES.Length];
        for (int i = 0; i < LuaConst.TAG_METHOD_NAMES.Length; i++)
            _tagMethodNames[i] = new LuaString(LuaConst.TAG_METHOD_NAMES[i]);
    }
    internal static LuaString GetReservedWord(int type) {
        if (type < 1 || type > reservedWordCount)
            throw new LuaErrorError($"Reserved word type out of range: {type}");
        return ReservedWords()[type - 1];
    }
    internal static LuaString[] ReservedWords() {
        if (reservedWords != null)
            return reservedWords;
        reservedWords = new LuaString[reservedWordCount];
        for (int type = 1; type <= reservedWordCount; type++) {
            string word             = LuaLexerUtils.GetTypeName(type);
            reservedWords[type - 1] = new LuaString(word, reserved: true);
        }
        return reservedWords;
    }

    /* ---------------- API ---------------- */

    public LuaCFunction SetPanic(LuaCFunction panicFunc) {
        LuaCFunction old = _panicFunc;
        _panicFunc       = panicFunc;
        return old;
    }
}

public partial class LuaState : LuaObject
{
    internal readonly LuaGlobalState globalState;
    private ThreadStatus             _threadStatus;
    internal ThreadStatus             prevThreadStatus; /* 给错误恢复函数用的，其他地方不要用，不可靠 */
    /* Stack */
    private List<LuaValue> _stack;
    private RawIdx         _top;       /* 下一个可用位置，当前使用到的最高元素的下个位置（总是满足 <= _currCI.Top） */
    private RawIdx         _last;      /* 栈最后一个空闲位置，后面还有 EXTRA_STACK 长度的备用空间 */
    private RawIdx         _errorFunc; /* 错误处理函数的 rawindex，为 -1 时表示没有错误处理函数，建议用 IsValidErrorFunc 判定是否有效 */
                                       /* 注：（这里 CLua 是用 0 表示是否无效，而且类型是指针地址偏移） */
    internal readonly bool isMainThread;
    /* CallInfo */
    private ushort   _ciLength;   /* ci 链表长度 */
    private CallInfo _headCI;     /* ci 链表头节点（也就是第一个 caller，但这个不算一个“调用”） */
    private CallInfo _currCI;     /* 当前调用 */
    private bool     _calledMark; /* 是否已进入内核调用，用于在 resume 时判断错误是否发生在内核，其他地方不要使用（不靠谱） */
    /* UpVal */
    private Upvalue  _openUpVals; /* 打开的上值链表，从更大的索引指向更小的索引 */
    private LuaState _twups;      /* threads with upvalues，线程链表，记录仍有未关闭上值的线程。空链表时，该引用指向自己 */
    /* Misc */
    private ushort    _nny;        /* 调用链中不可 yield 函数的数量，仅当 nny 为 0 时可以 yield；number of non-yieldable calls in stack */
    private ushort    _pcallCount; /* pcall 次数，在 resume 和 pcall 中控制。注意，仅在当前线程有效 */
    internal ushort   cCalls;      /* 嵌套层次，防止宿主栈溢出 */
    internal CallInfo _vmCI;       /* 调试用，获得 vm 当前的调用信息 */

    private LuaState() { /* 新建状态（主线程），用 NewState 来创建 */
        isMainThread = true;
        _type        = LuaConst.MarkLuaObject(LuaConst.TTHREAD);
        globalState  = new LuaGlobalState(this);
        _PreInitThread();
        if (_RawPCall(_DoInitState, IntPtr.Zero) != ThreadStatus.OK) {
            CloseState();
        }
    }
    public LuaState(LuaState from) { /* 新建线程 */
        LuaDebug.AssertNotNull(from);
        isMainThread = false;
        _type        = LuaConst.MarkLuaObject(LuaConst.TTHREAD);
        globalState  = from.globalState;
        from.PushStack(this);
        _PreInitThread();
        _InitStack();
    }

    ~LuaState() {
        if (isMainThread) { /* lua_close */
            CloseState();
        } else { /* luaE_freethread */
            CloseUpvalues((RawIdx)0);
            LuaDebug.Check(_openUpVals == null);
        }
    }

    /* Init helper */
    private void _InitStack() {
        _stack  = Enumerable.Repeat(LuaValue.NIL, LuaConfig.BASIC_STACK_SIZE).ToList();
        _last   = new RawIdx(_StackSize - LuaConfig.EXTRA_STACK - 1);
        _currCI = _headCI = new CallInfo(this); /* 注意分配了 ci 但不增加 _ciLength，这块 ci 是初始化后给宿主侧用的 */
        _currCI.Func      = _top;
        _top++;
        _currCI.Top = _top + LuaConst.MINSTACK;
    }
    private void _PreInitThread() {
        _threadStatus = ThreadStatus.OK;

        _stack = null;
        _top = _last = (RawIdx)0;
        _errorFunc   = RawIdx.InvalidErrorFunc;

        _currCI = _headCI = null;
        _ciLength         = 0;
        _calledMark       = false;

        _openUpVals = null;
        _twups      = this;

        _nny        = 1; /* 初始时不能 yield */
        _pcallCount = 0;
        cCalls      = 0;
        _vmCI       = null;
    }
    public void CloseState() {
        LuaState mainThread = globalState.mainThread;
        mainThread.CloseUpvalues((RawIdx)0);
        mainThread._stack  = null;
        mainThread._headCI = mainThread._currCI = null;
        mainThread._openUpVals                  = null;
        mainThread._twups                       = null;
#if DEBUG
        globalState.Logger?.Flush();
        globalState.Logger = null;
#endif
    }
    private static void _DoInitState(LuaState state, IntPtr ud) {
        state._InitStack();
        state.globalState._Init();
    }

    public static LuaState NewState() {
        LuaState state = new LuaState();
        if (state._threadStatus == ThreadStatus.OK)
            return state;
        return null;
    }
    public static LuaState NewState(string stateName, string outputDir) { /* 有 logger 的版本 */
        LuaState state = new LuaState();
#if DEBUG
        if (state != null && state._threadStatus == ThreadStatus.OK)
            state.globalState.Logger = new LuaLogger(stateName, outputDir);
#endif
        return state;
    }
    public static LuaState NewThread(LuaState from) {
        return new LuaState(from);
    }

    public override string ToString() {
        return $"<th status: {_threadStatus}, top: {_top}, last: {_last}, errorFunc: {_errorFunc}, isMainThread: {isMainThread}, ciLength: {_ciLength}, calledMark: {_calledMark} nny: {_nny}, pcallCount: {_pcallCount}, cCalls: {cCalls}>";
    }

    /* ---------------- Properties ---------------- */

    private int     _StackSize => _stack.Count;
    internal RawIdx Top { /* 申请空间请用 IncreaseTop，会做空余空间检查 */
        get => _top;
        set {
            CheckValidRawIdx(value);
            _top = value;
        }
    }
    private int _UsingSize { /* 调用链上最大的 top，即当前已用空间的大小 */
        get {
            RawIdx maxTop = _top;
            for (CallInfo ci = _currCI; ci != null; ci = ci.prev)
                maxTop = (RawIdx)Math.Max((int)maxTop, (int)ci.Top);
            CheckCorrectTopPos(maxTop);
            return (int)maxTop;
        }
    }
    public bool         Yieldable    => _nny == 0;
    public bool         InResume     => Yieldable; /* 如果 yieldable 就说明当前处于 resume 的调用链上 */
    public bool         InPCall      => _pcallCount > 0;
    public ThreadStatus ThreadStatus => _threadStatus;
    public ThreadStatus PrevThreadStatus => prevThreadStatus;
    public int CallLevel {
        get {
            int level = 0;
            for (CallInfo ci = _currCI; ci != _headCI; ci = ci.prev)
                level++;
            return level;
        }
    }
    internal CallInfo   CurrCI       => _currCI;
    internal CallInfo   HeadCI       => _headCI;

    /* ---------------- Stack ---------------- */

    /* Index Utils */
    internal bool IsValidStackLdx(int ldx) { /* ldx 是否为栈上元素（不是伪索引） */
        if (ldx > 0)
            return ldx <= _top - _currCI.Func - 1;
        else
            return ldx != 0 && !IsPseudoLdx(ldx) && -ldx <= _top - _currCI.Func - 1;
    }
    internal bool ToLdx(RawIdx rawIdx, out int resultLdx) {
        resultLdx = 0;
        if (rawIdx > _currCI.Func && rawIdx < _top && rawIdx <= _currCI.Top) {
            resultLdx = rawIdx - _currCI.Func;
            return true;
        }
        return false;
    }
    internal bool ToRawIdx(int ldx, out RawIdx result) {
        result = (RawIdx)0;
        if (!IsValidStackLdx(ldx))
            return false;
        if (ldx > 0)
            result = _currCI.Func + ldx;
        else /* ldx < 0 */
            result = _top + ldx;
        return true;
    }
    internal static bool IsPseudoLdx(int ldx) {
        return ldx <= LuaConst.REGISTRYINDEX;
    }
    internal static bool IsUpvalueLdx(int ldx) {
        return ldx < LuaConst.REGISTRYINDEX;
    }
    internal bool IsValidRawIdx(RawIdx rawIdx) {
        return rawIdx >= (RawIdx)0 && rawIdx < (RawIdx)_StackSize;
    }

    /* Inspect and Modify Stack */

    internal LuaValue GetStack(RawIdx rawIdx) {
        return _stack[(int)rawIdx];
    }
    internal void PushStack(IntPtr l) {
        _stack[(int)_top++] = new LuaValue(l);
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(bool b) {
        _stack[(int)_top++] = b ? LuaValue.TRUE : LuaValue.FALSE;
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(int i) {
        PushStack((long)i);
    }
    internal void PushStack(long i) {
        _stack[(int)_top++] = new LuaValue(i);
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(double n) {
        _stack[(int)_top++] = new LuaValue(n);
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(LuaCFunction func) {
        _stack[(int)_top++] = new LuaValue(func);
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(LuaObject obj) {
        _stack[(int)_top++] = new LuaValue(obj);
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(string s) {
        _stack[(int)_top++] = new LuaValue(s);
        CheckCorrectTopPosInAPI();
    }
    internal void PushStack(in LuaValue value) {
        _stack[(int)_top++] = value;
        CheckCorrectTopPosInAPI();
    }
    internal void SafePushStack(IntPtr l) {
        _CheckStack(1);
        PushStack(l);
    }
    internal void SafePushStack(bool b) {
        _CheckStack(1);
        PushStack(b);
    }
    internal void SafePushStack(int i) {
        SafePushStack((long)i);
    }
    internal void SafePushStack(long i) {
        _CheckStack(1);
        PushStack(i);
    }
    internal void SafePushStack(double n) {
        _CheckStack(1);
        PushStack(n);
    }
    internal void SafePushStack(LuaCFunction func) {
        _CheckStack(1);
        PushStack(func);
    }
    internal void SafePushStack(LuaObject obj) {
        _CheckStack(1);
        PushStack(obj);
    }
    internal void SafePushStack(string s) {
        _CheckStack(1);
        PushStack(s);
    }
    internal void SafePushStack(in LuaValue value) {
        _CheckStack(1);
        PushStack(value);
    }
    internal void UnSafePushStack(in LuaValue value) {
        _stack[(int)_top++] = value;
    }

    internal void IncreaseTop(int step = 1) {
        _CheckStack(step);
        Top += step;
    }

    internal void _CheckStack(int needSpace, Action beforeGrowStack, Action afterGrowStack) {
        if (_last - _top <= needSpace) {
            beforeGrowStack();
            GrowStack(needSpace); /* 不够的话就直接增长这么多 */
            afterGrowStack();
        }
    }
    internal void _CheckStack(int needSpace) {
        _CheckStack(needSpace, () => { }, () => { });
    }
    /* 只改变空闲空间大小，不改变空闲空间和预留空间。 */
    internal void ResizeStack(int newSize) {
        LuaDebug.Assert(newSize <= LuaConfig.LUAI_MAXSTACK || newSize == LuaConfig.ERROR_STACK_SIZE);
        LuaDebug.Assert(newSize - (int)_top - LuaConfig.EXTRA_STACK > 0, $"New size too small: {newSize}.");
        int diff = newSize - _StackSize;
        if (diff > 0) {
            for (int i = 0; i < diff; i++)
                _stack.Add(LuaValue.NIL);
        } else if (diff < 0) {
            _stack.RemoveRange(newSize, -diff);
        }
        _last = (RawIdx)newSize - LuaConfig.EXTRA_STACK - 1;
        CheckCorrectLastPos();
    }
    /* 会优先尝试翻倍整个栈容量 */
    internal void GrowStack(int growSpace) {
        if (_StackSize > LuaConfig.LUAI_MAXSTACK)
            throw new LuaErrorError("Error after extra size?");
        int minimumSize = (int)_top + growSpace + LuaConfig.EXTRA_STACK;
        int newSize     = _StackSize * 2; /* 每次翻倍 */

        newSize = Math.Min(newSize, LuaConfig.LUAI_MAXSTACK);
        newSize = Math.Max(newSize, minimumSize);
        if (newSize > LuaConfig.LUAI_MAXSTACK) {
            ResizeStack(LuaConfig.ERROR_STACK_SIZE);
            throw new LuaStackOverflow($"grow too much, newSize: {newSize}");
        } else {
            ResizeStack(newSize);
        }
    }
    /* 会以特殊算法决定缩减量，可能无变化 */
    internal void ShrinkStack() {
        int usingSize = _UsingSize;
        int goodSize  = usingSize + (usingSize / 8) + 2 * LuaConfig.EXTRA_STACK; /* 公式来自 luaD_shrinkstack */
        goodSize      = Math.Min(goodSize, LuaConfig.LUAI_MAXSTACK);
        if (_StackSize > LuaConfig.LUAI_MAXSTACK) /* had been handling stack overflow? */
            FreeCI();                             /* free all CIs (list grew because of an error) */
        else
            ShrinkCI();
        if (usingSize <= (LuaConfig.LUAI_MAXSTACK - LuaConfig.EXTRA_STACK) && goodSize < _StackSize)
            ResizeStack(goodSize);
    }

    /* ---------------- Manipulate CallInfo ---------------- */

    /* 尾插一个空 ci，即插在 _currCI 后边 */
    internal CallInfo ExtendCI() {
        CallInfo newCI = new CallInfo(this);
        LuaDebug.Assert(_currCI.next == null, "ExtendCI: _currCI.next is not null. Should check before extend.");
        _currCI.next = newCI;
        newCI.prev   = _currCI;
        _ciLength++;
        return newCI;
    }
    /* 释放全部未使用的 CI */
    internal void FreeCI() {
        CallInfo curr = _currCI;
        CallInfo next = _currCI.next;
        while (next != null) { /* 其实对于 C# 来说释放链表不需要遍历，但既然要统计长度就顺便把指针都设为 null 吧 */
            curr.next = null;
            curr      = next;
            next      = next.next;
            _ciLength--;
        }
    }
    /* 释放一半未使用的 ci */
    internal void ShrinkCI() {
        CallInfo curr = _currCI;
        CallInfo next2;
        while (curr.next != null && curr.next.next != null) {
            next2      = curr.next.next;
            curr.next  = next2;
            next2.prev = curr;
            curr       = next2;
            _ciLength--;
        }
    }
    internal CallInfo NewCI() {
        _currCI = _currCI.next == null ? ExtendCI() : _currCI.next;
        return _currCI;
    }

    /* ---------------- Debug ---------------- */

    [Conditional("DEBUG")]
    internal void CheckValidRawIdx(RawIdx rawIdx) {
        LuaDebug.Check(IsValidRawIdx(rawIdx), $"Raw index out of range: {rawIdx}");
    }
    [Conditional("DEBUG")]
    internal void CheckValidElem(in LuaValue elem) {
        LuaDebug.Check(elem.Valid, $"Not valid stack elem (none). Current type: {elem.Type}.");
    }
    [Conditional("DEBUG")]
    internal void CheckStackLdxAndElem(int ldx, in LuaValue elem) {
        CheckValidElem(elem);
        LuaDebug.Check(!IsPseudoLdx(ldx), $"Can't be pseudo index: {ldx}.");
    }
    [Conditional("DEBUG")]
    internal void CheckCorrectLastPos() {
        LuaDebug.Check(
            _last == (RawIdx)_StackSize - LuaConfig.EXTRA_STACK - 1,
            $"_last is not on the right position. last: {_last}, stack size: {_StackSize}."
        );
    }
    [Conditional("DEBUG")]
    internal void AssertCorrectTopPos() {
        CheckCorrectTopPos(_top);
    }
    [Conditional("DEBUG")]
    internal void CheckCorrectTopPos(RawIdx targetTop) {
        LuaDebug.Check(targetTop <= _last, $"TargetTop is out of range. Target top: {targetTop}, last: {_last}.");
    }
    [Conditional("DEBUG")]
    internal void CheckCorrectTopPosInAPI() {
        /* _top 是否溢出栈帧限制，主要用于公开接口中检查用户操作 */
        LuaDebug.Check(_top <= _currCI.Top, $"Stack overflow. stack top: {_top}, curr ci top: {_currCI.Top}");
    }
}

}
