/* 即，CLua 里的 ldo.c */
namespace YALuaToy.Core {

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using YALuaToy.Const;
using YALuaToy.Debug;

using InstructionIndex = System.Int32; /* 指令索引 */

internal delegate void PFunction(LuaState state, IntPtr ctx);

/// <summary>所有权只由 LuaState 持有</summary>
internal class CallInfo
{
    /* 通用数据 */
    public readonly LuaState state;
    private RawIdx           _func;      /* 调用栈首元素指针 */
    private RawIdx           _top;       /* 调用栈栈顶，总是 >= state._top，栈的 _top 不能超过该顶 */
    internal CallInfo        prev, next; /* 调用链，注意要定期释放 _next */
    private short            _resultCount;
    private CallStatus       _callStatus;

    /*
     * 两个用途：
     * 1.  Yield 时，记录被 yield 栈的实际栈帧起点，resume 时要用这个起点
     *     之所以会修改，是因为 yield 返回时通过修改栈帧起点来模拟“返回值被压入栈”的情形
     * 2.  PCall 时，假设有调用函数 F 和延续函数 K，该字段记录 F 的栈帧起点，这样在发生错误时，就能恢复 F 栈帧并执行 K
     */
    public RawIdx extra;

    /* Lua 函数数据 */
    private LClosure _lclosure;
    private RawIdx   _firstArg; /* Lua 闭包实际栈帧起点（即，第一个固定参数位置，不一定等于 _func+1 */
    private InstructionIndex      _pc;   /* 下一条指令地址 */

    /* 宿主函数数据 */
    private LuaKFunction _kFunc; /* 宿主函数的延续函数 */
    private RawIdx       _oldErrorFunc;
    private IntPtr       _ctx; /* 宿主函数的延续数据 */

    public CallInfo(LuaState state) {
        this.state = state;
        _func         = (RawIdx)0;
        _top          = (RawIdx)0;
        prev = next  = null;
        _resultCount = 0;
        _callStatus  = 0;
    }

    public override string ToString() {
        return $"<ci isLua: {IsLua}, func: {_func}, top: {_top}, resultCount: {_resultCount}, callStatus: {_callStatus}, extra: {extra}, base: {_firstArg}, pc: {_pc}, kFunc: {_kFunc}, oldErrorFunc: {_oldErrorFunc}, ctx: {_ctx}>";
    }

    /* 因为 CallInfo 可能复用，所以独立一个函数出来单独设置吧 */
    public void Reset(short resultCount, RawIdx func, RawIdx top, CallStatus callStatus = 0) {
        _resultCount = resultCount;
        _func        = func;
        _top         = top;
        _callStatus  = callStatus;
    }
    public void ResetLuaInfo(RawIdx firstArg, RawIdx func, int pc) {
        _firstArg     = firstArg;
        _pc       = pc;
        _lclosure = state.GetStack(func).LObject<LClosure>();
    }
    public void ResetHostInfo(LuaKFunction kFunc, RawIdx oldErrorFunc, IntPtr ctx) {
        _oldErrorFunc = oldErrorFunc;
        SetContinuation(kFunc, ctx);
    }
    public void SetContinuation(LuaKFunction kFunc, IntPtr ctx) {
        _kFunc = kFunc;
        _ctx   = ctx;
    }

    /* ---------------- Properties ---------------- */

    internal RawIdx Func { /* 指向栈帧函数位置，参数要 +1 */
        get => _func;
        set => _func = value;
    }
    internal RawIdx Top {
        get => _top;
        set => _top = value;
    }
    internal CallStatus CallStatus {
        get => _callStatus;
        set => _callStatus = value;
    }
    public bool         IsLua       => GetCallStatusFlag(CallStatus.LUA);
    public LuaValue     FuncValue   => state.GetStack(_func);
    public short        ResultCount => _resultCount;
    public LuaKFunction KFunc {
        get => _kFunc;
        set => _kFunc = value;
    }
    public RawIdx OldErrorFunc => _oldErrorFunc;
    public IntPtr CTX {
        get => _ctx;
        set => _ctx = value;
    }
    public LClosure LClosure => _lclosure;
    public RawIdx   FirstArg     => _firstArg;
    internal InstructionIndex PC {
        get => _pc;
        set => _pc = value;
    }
    public Instruction NextInst => GetInstruction(_pc); /* pc 指向下一个未执行的指令，所以叫 'NextInst' */

    /* ---------------- Utils ---------------- */

    public Instruction GetInstruction(InstructionIndex idx) {
        return _lclosure.proto.GetInstruction(idx);
    }

    public bool GetCallStatusFlag(CallStatus status) {
        return (_callStatus & status) != 0;
    }
    public void SetCallStatusFlag(CallStatus status, bool value) {
        if (value)
            _callStatus |= status;
        else
            _callStatus &= ~status;
    }
}

public partial class LuaState
{
    /* ---------------- Do Call ---------------- */

    /* 因为 PCall 依赖 Call 实现，而 C# 异常不是非局部跳转，导致这里做了很多特殊处理 */
    internal void _Call(RawIdx func, short resultCount, bool noYield = false) {
        LuaDebug.Assert(isMainThread || InResume, "Threads can only be started by resume");
        if (noYield)
            _nny++;

        if (++cCalls >= LuaConfig.LUAI_MAXCCALLS) {
            if (cCalls == LuaConfig.LUAI_MAXCCALLS)
                throw new LuaStackOverflow("C stack overflow.");
            else if (cCalls >= LuaConfig.LUAI_MAXCCALLS + LuaConfig.LUAI_MAXCCALLS / 10)
                throw new LuaErrorError("Error while handling stack error.");
        }

        ThreadStatus threadStatus = _Try(() => {
            /* 如果是宿主函数则在 _PreCall 内完成调用，否则则是 Lua 函数，通过 Execute 完成调用 */
            if (!_PreCall(func, resultCount))
                Execute();
        }, out Exception exception);

        cCalls--;
        if (noYield)
            _nny--;

        if (threadStatus != ThreadStatus.OK) {
            /* 这里实际上可能受到 yield，因为 C# 异常是栈展开的，不像 CLua 那样直接非局部跳转。遇到 yield 就继续向外抛出即可 */
            if (LuaUtils.IsErrorStatus(threadStatus) && isMainThread && !InPCall) { /* 主线程的 Call，致命错误 */
                _threadStatus = threadStatus; /* Call 遇到无法恢复的错误时，设置线程状态为死亡（异常状态表示线程已死，无法再用） */
                if (globalState.PanicFunc != null) {
                    if (_currCI.Top < _top)
                        _currCI.Top = _top;
                    globalState.PanicFunc(this);
                }
                throw new LuaException($"Lua thread abort.");
            } else if (exception is RethrowingWrapError rethrowWrapException) {
                throw rethrowWrapException; /* 继续重抛出，不用担心抛到线程外，因为线程要么是主线程，要么被 resume 启动（保护调用） */
            } else {
                throw new RethrowingWrapError(exception); /* 继续重抛出，不用担心抛到线程外，原因同上 */
            }
        }
    }
    internal ThreadStatus _PCall(PFunction pfunc, IntPtr ctx, RawIdx oldTop) {
        return _PCall(pfunc, ctx, oldTop, RawIdx.InvalidErrorFunc);
    }
    internal ThreadStatus _PCall(PFunction pfunc, IntPtr ctx, RawIdx oldTop, RawIdx errorFunc) {
        LuaDebug.Assert(isMainThread || InResume, "Threads can only be started by resume");
        CallInfo oldCI        = _currCI;
        ushort   oldNNY       = _nny;
        RawIdx   oldErrorFunc = _errorFunc;
        _errorFunc            = errorFunc;
        _pcallCount++;
        ThreadStatus threadStatus = _RawPCall(pfunc, ctx);
        _pcallCount--;
        if (threadStatus != ThreadStatus.OK) {
            CloseUpvalues(oldTop);
            _RestoreOldTopWhenError(oldTop);
            _currCI = oldCI;
            _nny    = oldNNY;
            ShrinkStack();
        }
        _errorFunc = oldErrorFunc;
        return threadStatus;
    }
    internal ThreadStatus _RawPCall(PFunction pfunc, IntPtr ctx) {
        ushort       oldCCalls    = cCalls;
        ThreadStatus threadStatus = _Try(() => { pfunc(this, ctx); });
        cCalls                    = oldCCalls;
        return threadStatus;
    }
    private ThreadStatus _Try(Action tryAction) {
        return _Try(tryAction, out _);
    }
    /* 关于 CLua 里 errorJmp->status == -1 的情况，这种情况呢是错误不由 luaD_throw 抛出导致的
       CLua 对内核的安全性非常自信（总是假设：内核要么没错，要么错误都被 luaD_throw 抛出），如果内核满足绝对安全，那么错误不由
       luaD_throw 抛出的情况就只剩下 resume 的准备函数里了，所 CLua 内核吧 errorJmp->status == -1 的情况理解为线程执行前
       就报错了，线程本身状态时好的，于是就进一步可以在 resume 里看到逻辑当 errorJmp->status == -1 时不做异常处理也不改变线程状态 */
    private ThreadStatus _Try(Action tryAction, out Exception exception) {
        /* 这里会自动把异常对象压入栈中并执行 _top++ */
        bool rethrowing           = false;
        exception                 = null;
        ThreadStatus threadStatus = ThreadStatus.OK;
        try {
            tryAction();
        } catch (RethrowingWrapError e) { /* 重抛出的错误，其异常信息已压入栈中，就不重复压入了 */
            rethrowing = true;
            if (e.error is LuaException luaException) {
                threadStatus = luaException.threadStatus;
            } else {
                threadStatus = ThreadStatus.ERRRUN;
            }
            exception = e;
        } catch (LuaException e) {
            threadStatus = e.threadStatus;
            if (!(e is LuaYield))
                UnSafePushStack(e.errorValue);
            exception = e;
        } catch (Exception e) {
            threadStatus = ThreadStatus.ERRRUN;
            UnSafePushStack(new LuaValue(e.Message));
            exception = e;
        }

        /* 按 CLua 行为，错误处理函数只在栈帧抛出错误时触发。重抛出的错误不属于当前栈帧就不触发了 */
        prevThreadStatus = threadStatus;
        if (!rethrowing && LuaUtils.IsErrorStatus(threadStatus) && LuaUtils.IsValidErrorFunc(_errorFunc) && !(exception is LuaStackOverflow)) {
            _stack[(int)_top]     = _stack[(int)_top - 1];
            _stack[(int)_top - 1] = _stack[(int)_errorFunc];
            _top++;
            _Try(() => { _Call(_top - 2, 1); }); /* 返回数量是 1，这意味着错误处理函数不能消耗错误对象（最多修改） */
        }
        return threadStatus;
    }

    /* 返回值表示是否已发出调用（即，是否为宿主函数） */
    private bool _PreCall(RawIdx func, short resultCount) {
        LuaValue funcValue = _stack[(int)func];
        switch (funcValue.Type.NotNoneVariant) {
        case LuaConst.TCCL:
            CClosure cclosure = funcValue.LObject<CClosure>();
            _calledMark       = true;
            _ExecuteCFunc(cclosure.func, func, resultCount);
            return true;
        case LuaConst.TLCF:
            _calledMark = true;
            _ExecuteCFunc(funcValue.LightFunc, func, resultCount);
            return true;
        case LuaConst.TLCL:
            _PrepareLCall(funcValue.LObject<LClosure>(), func, resultCount);
            return false;
        default:
            /* 这里尝试获取元方法 '__call'，然后调用该方法 */
            _CheckStack(1);
            _GetFuncTM(func);
            return _PreCall(func, resultCount);
        }
    }
    /* 返回 false 表示有多返回值 MULTRET，否则返回 true */
    private bool _PosCall(CallInfo ci, RawIdx firstResult, int realResultCount) {
        int    wantedResultCount = ci.ResultCount;
        RawIdx destFirstResult   = ci.Func;
        _currCI                  = _currCI.prev;
        return _MoveResults(firstResult, destFirstResult, realResultCount, wantedResultCount);
    }

    /* call utils */

    private void _ExecuteCFunc(LuaCFunction cfunc, RawIdx func, short resultCount) {
        _CheckStack(LuaConst.MINSTACK); /* 原生函数调用时至少分配的空间 */
        CallInfo ci = NewCI();
        ci.Reset(resultCount, func, _top + LuaConst.MINSTACK); /* 注意这里没有修改 _top，而是只改了 ci->top */
        CheckCorrectTopPos(ci.Top);
        int realResultCount = cfunc(this);
        CheckArgOrResultCount(realResultCount);
        _PosCall(ci, _top - realResultCount, realResultCount);
    }
    private void _PrepareLCall(LClosure lclosure, RawIdx func, short resultCount) {
        LuaProto proto          = lclosure.proto;
        int      actualArgCount = _top - func - 1; /* 实际推入栈中的参数数量，包括固定参数和 vararg */
        int      frameSize      = proto.FrameSize;
        _CheckStack(frameSize); /* 申请扩展栈空间 */

        /* 移动参数到合适位置 */
        RawIdx firstArg;
        if (proto.Vararg)
            firstArg = _AdjustVarargs(proto, actualArgCount);
        else {
            /* 没有 vararg 的话，直接复用压入参数的区域，补固定参数即可 */
            for (int i = actualArgCount; i < proto.ParamCount; i++)
                _stack[(int)_top++] = LuaValue.NIL;
            firstArg = func + 1;
        }
        _top = firstArg + frameSize;
        AssertCorrectTopPos();

        /* 新建 callinfo */
        CallInfo ci = NewCI();
        ci.Reset(resultCount, func, _top);
        ci.ResetLuaInfo(firstArg, func, 0);
        ci.SetCallStatusFlag(CallStatus.LUA, true);
    }
    private RawIdx _AdjustVarargs(LuaProto proto, int actualArgCount) {
        /* 将参数复制到正确位置，具体来说就是把固定参数复制到 vararg 后面，然后以第一个固定参数为 frameBase */
        int    fixedArgCount = proto.ParamCount;
        RawIdx frameBase     = _top;
        RawIdx firstFixedArg = _top - actualArgCount;
        int    i;
        for (i = 0; i < fixedArgCount && i < actualArgCount; i++) {
            _stack[(int)_top++]            = _stack[(int)firstFixedArg + i]; /* 把固定参数拷到 vararg 后面 */
            _stack[(int)firstFixedArg + i] = LuaValue.NIL;
        }
        for (; i < fixedArgCount; i++) /* 固定参数不足，用 nil 补 */
            _stack[(int)_top++] = LuaValue.NIL;
        return frameBase;
    }
    private void _GetFuncTM(RawIdx func) {
        /* 尝试获得 '__call' 元方法，若存在且是方法，则压入栈 */
        LuaValue tagMethod = GetTagMethod(_stack[(int)func], TagMethod.CALL);
        if (!LuaType.CheckTag(tagMethod.Type, LuaConst.TFUNCTION))
            throw new LuaUnexpectedType(tagMethod.Type, LuaConst.TFUNCTION);
        for (int i = (int)_top; i > (int)func; i--) /* 把包括 func 之后的元素向后挪一格 */
            _stack[i] = _stack[i - 1];
        _top++;
        _stack[(int)func] = tagMethod; /* 写回 func */
    }
    private bool _MoveResults(RawIdx currFirstResult, RawIdx destFirstResult, int currResultCount, int wantedResultCount) {
        /* 返回 false 表示有多返回值 MULTRET，否则返回 true */
        switch (wantedResultCount) {
        case 0:
            break;
        case LuaConst.MULTRET:
            for (int i = 0; i < currResultCount; i++)
                _stack[(int)destFirstResult + i] = _stack[(int)currFirstResult + i];
            _top = destFirstResult + currResultCount;
            return false;
        default: /* 一般情况 */
            LuaDebug.Check(wantedResultCount > 0, $"Invalid wanted result count: {wantedResultCount}");
            if (wantedResultCount <= currResultCount) {
                for (int i = 0; i < wantedResultCount; i++)
                    _stack[(int)destFirstResult + i] = _stack[(int)currFirstResult + i];
            } else {
                for (int i = 0; i < currResultCount; i++)
                    _stack[(int)destFirstResult + i] = _stack[(int)currFirstResult + i];
                for (int i = currResultCount; i < wantedResultCount; i++)
                    _stack[(int)destFirstResult + i] = LuaValue.NIL;
            }
            break;
        }
        _top = destFirstResult + wantedResultCount;
        return true;
    }
    private void _RestoreOldTopWhenError(RawIdx oldTop) {
        _stack[(int)oldTop] = _stack[(int)_top - 1];
        _top                = oldTop + 1;
    }

    /* ---------------- Coroutines ---------------- */

    public ThreadStatus Resume(LuaState from, short argCount) {
        ushort oldnny        = _nny;
        ushort oldPcallCount = _pcallCount;

        /* Resume 本身就提供保护调用，所以函数内不能直接抛出错误，如果在进入 RawPCall 前
           发生错误，就用该函数压入错误对象（以模拟 RawPCall 的行为），并恢复栈顶 */
        ThreadStatus HandleResumeError(string msg) {
            _top -= argCount;
            _stack[(int)_top++] = new LuaValue(msg); /* 这里没用 PushStack 是不想再触发检测了，省的弹出更多错误信息 */
            return ThreadStatus.ERRRUN;
        }

        if (_threadStatus == ThreadStatus.OK) { /* 正在运行的线程是无法 resume 的 */
            if (_currCI != _headCI)             /* 不过，未执行的线程可以 resume（典型的：线程刚分配压入函数时，用 resume 启动） */
                return HandleResumeError("Cannot resume non-suspended coroutine.");
        } else if (_threadStatus != ThreadStatus.YIELD) {
            return HandleResumeError("cannot resume dead coroutine");
        }

        cCalls = (ushort)(from != null ? from.cCalls + 1 : 1);
        if (cCalls >= LuaConfig.LUAI_MAXCCALLS)
            return HandleResumeError("C stack overflow");
        _nny = 0;      /* 刚 resuem 的线程是可以 yieldable 的 */
        _pcallCount++; /* 刚 resume 的线程保护调用数量直接就是 1，因为 resume 就是保护调用 */

        /* 如果是 OK（即，新建的线程），则要加上启动函数；否则只是重启挂起的线程，只需要参数即可 */
        CheckArgOrResultCount(_threadStatus == ThreadStatus.OK ? argCount + 1 : argCount);

        _calledMark               = false;
        ThreadStatus threadStatus = _RawPCall(_DoResume, argCount);
        if (_calledMark) { /* 如果是内核异常，则尝试恢复并以栈展开的形式调用连续函数 */
            while (_calledMark && LuaUtils.IsErrorStatus(threadStatus) && _Recover(threadStatus)) {
                _calledMark  = false;
                threadStatus = _RawPCall((LuaState state, IntPtr ctx) => { state._Unroll(ctx); }, (int)threadStatus);
            }
            if (LuaUtils.IsErrorStatus(threadStatus)) { /* 恢复失败，RawPCall 保证报错时错误信息被压入栈 */
                _threadStatus = threadStatus;
                CheckArgOrResultCount(1); /* 这里相比 CLua 少了一步 seterrorobj，因为在 RawPCall 里面处理了 */
                _currCI.Top = _top;
            } else {
                LuaDebug.Assert(
                    threadStatus == _threadStatus, $"Unexpected thread status. pcall status: {_threadStatus}, thread status: {threadStatus}"
                );
            }
        }
        LuaDebug.Check(_pcallCount == oldPcallCount + 1, $"Incorrect pcall count: {_pcallCount}, {oldPcallCount}.");
        _calledMark = false;
        _pcallCount = oldPcallCount;
        _nny        = oldnny;
        cCalls--;
        LuaDebug.Check(cCalls == (from != null ? from.cCalls : 0), $"Incorrect cCalls count: {cCalls}.");
        return threadStatus;
    }
    private static void _DoResume(LuaState state, IntPtr ctx) {
        int      argCount = checked((int)ctx); /* IntPtr 是指针长度，可能是 64 位 */
        RawIdx   firstArg = state._top - argCount;
        CallInfo currCI   = state._currCI;

        if (state._threadStatus == ThreadStatus.OK) {            /* 新建线程 */
            if (!state._PreCall(firstArg - 1, LuaConst.MULTRET)) /* argCount-1 是线程启动函数的位置 */
                state.Execute();
        } else { /* 恢复挂起线程 */
            LuaDebug.Check(state._threadStatus == ThreadStatus.YIELD, $"Unexpected thread status: {state._threadStatus}");
            state._threadStatus = ThreadStatus.OK; /* 恢复为 OK */
            currCI.Func            = currCI.extra;    /* 恢复栈帧起点 */
            if (currCI.IsLua) {
                /* CLua 中，只有在 hook 时可能导致 Lua 中调用 resume；但当前 C# 版本没有实现 hook，不应该在 Lua 中执行到这里 */
                LuaDebug.Check(false, "It's impossible to resume in Lua function.");
                state.Execute();
            } else {
                int resultCount = argCount; /* 如果没有连续函数，那压入的参数不动 */
                if (currCI.KFunc != null) { /* 是否在 yield 中指定了连续函数 */
                    /* 这里没有用 precall 来执行延续函数，用 precall 会破坏上面已经准备好的调用信息 */
                    resultCount = currCI.KFunc(state, ThreadStatus.YIELD, currCI.CTX);
                    state.CheckArgOrResultCount(resultCount);
                    firstArg = state._top - resultCount;
                }
                /* 用 PosCall 处理返回结果（如果有 KFunc 那就是处理 KFunc 的，否则就是处理原本发出 yield 的那个函数） */
                state._PosCall(currCI, firstArg, resultCount);
            }
            state._Unroll((IntPtr)ThreadStatus.OK);
        }
    }
    private void _Unroll(IntPtr ud) {
        /* 沿着调用链一路执行其延续函数，以结束整个调用 */
        ThreadStatus threadStatus = (ThreadStatus)ud;
        if (LuaUtils.IsErrorStatus(threadStatus))
            _FinishCCall(threadStatus);
        while (_currCI != _headCI) {
            if (_currCI.IsLua) {
                FinishOp();
                Execute();
            } else {
                _FinishCCall(ThreadStatus.YIELD);
            }
        }
    }
    private bool _Recover(ThreadStatus threadStatus) { /* 返回是否恢复成功 */
        /* Find PCall */
        CallInfo pcallCI = null;
        for (CallInfo ci = _currCI; ci != null; ci = ci.prev) {
            if (ci.GetCallStatusFlag(CallStatus.YIELDABLE_PCALL)) {
                pcallCI = ci;
                break;
            }
        }
        if (pcallCI == null)
            return false;
        RawIdx func = pcallCI.extra; /* 报错时恢复 PCall 时记录的真起点 */
        CloseUpvalues(func);
        _RestoreOldTopWhenError(func);
        _currCI = pcallCI;
        _nny    = 0; /* 恢复 yieldable */
        ShrinkStack();
        _errorFunc = pcallCI.OldErrorFunc;
        return true;
    }
    private void _FinishCCall(ThreadStatus threadStatus) {
        /* 结束一个中断的宿主函数，调用它的连续函数 */

        /* 确保当前可以调用连续函数 */
        LuaDebug.Check(_currCI.KFunc != null && _nny == 0, "Invalid kfunc or nny");
        /* 如果是被 resume 调用，则 threadStatus 为 YIELD；否则则是被 Recover 调用，从错误中恢复
           YIELDABLE_PCALL 表示 Recover 函数在调用链上找到了 PCall */
        LuaDebug.Check(
            _currCI.GetCallStatusFlag(CallStatus.YIELDABLE_PCALL) || threadStatus == ThreadStatus.YIELD,  //
            "Can't recover because no pcall in call chain."
        );

        /* 如果是从 pcall 恢复，则重置 callstatus 并把老的 errorfunc 恢复到当前状态 */
        if (_currCI.GetCallStatusFlag(CallStatus.YIELDABLE_PCALL)) {
            _currCI.SetCallStatusFlag(CallStatus.YIELDABLE_PCALL, false);
            _errorFunc = _currCI.OldErrorFunc;
        }

        /* 这一步说实话有点没看懂 */
        _AdjustResult(_currCI.ResultCount);

        _calledMark     = true;
        int resultCount = _currCI.KFunc(this, threadStatus, _currCI.CTX);
        CheckArgOrResultCount(resultCount);
        _PosCall(_currCI, _top - resultCount, resultCount);
    }

    public int Yield(short resultCount) {
        return Yield(resultCount, IntPtr.Zero, null);
    }
    public int Yield(short resultCount, IntPtr ctx, LuaKFunction kfunc) {
        CheckArgOrResultCount(resultCount);
        if (!Yieldable) {
            if (this == globalState.mainThread) /* 主线程上不能 yield */
                throw new LuaRuntimeError("Attempt to yield from outside a coroutine.");
            else /* 调用链上某个宿主函数没有连续函数，无法 yield */
                throw new LuaRuntimeError("Attempt to yield across a C-call boundary.");
        }
        _threadStatus = ThreadStatus.YIELD;
        _currCI.extra = _currCI.Func;
        if (_currCI.IsLua) {
            /* Lua 内不支持 resume 或 yield，如果发生在 Lua 函数内，则只有可能是 hook。但本实现目前不支持 hook，所以不可能 */
            LuaDebug.Check(kfunc == null, "It's impossible to yield in Lua function.");
        } else {
            _currCI.KFunc = kfunc;
            if (kfunc != null)
                _currCI.CTX = ctx;
            throw new LuaYield();
        }
        LuaDebug.Check(false, "It's impossible to get in here which should be return by throw a yield.");
        return 0;
    }

    /* ---------------- Utils ---------------- */

    private void _AdjustResult(short resultCount) {
        if (resultCount == LuaConst.MULTRET && _currCI.Top < _top)
            _currCI.Top = _top;
    }

    [Conditional("DEBUG")]
    internal void CheckArgOrResultCount(int atLeast) {
        /* 该断只能在执行 _PosCall 前调用，这样栈帧底部的 func 就还没覆盖掉。该断言总是忽略栈帧底部的 func */
        LuaDebug.Check(atLeast < (_top - _currCI.Func), $"Not enough parameters in the stack. At least: {atLeast}");
    }
    [Conditional("DEBUG")]
    internal void CheckElemCount(int atLeast) {
        /* 通用的栈帧元素数量检查，栈底的 func 也算元素 */
        LuaDebug.Check(atLeast <= (_top - _currCI.Func), $"Not enough elements in the stack (including func). At least: {atLeast}");
    }
}

}
