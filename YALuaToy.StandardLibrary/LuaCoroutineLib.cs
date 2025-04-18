namespace YALuaToy.StandardLibrary {

using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using YALuaToy.Core;
using YALuaToy.Const;
using System.Security.Cryptography;

internal static class LuaCoroutineLib
{
    public const string LIB_NAME = "coroutine";

    private static readonly List<(string, LuaCFunction)> coroutineFuncs = [
        ("create", _Create),
        ("resume", _Resume),
        ("running", _Running),
        ("status", _Status),
        ("wrap", _Wrap),
        ("yield", _Yield),
        ("isyieldable", _Yieldable),
    ];

    internal static int OpenLib_Coroutine(this LuaState state) {
        state.NewLib(coroutineFuncs);
        return 1;
    }

    /* ---------------- Lib Funcs ---------------- */

    private static int _Create(this LuaState state) {
        state.CheckArgType(1, LuaConst.TFUNCTION);
        LuaState newThread = LuaState.NewThread(state);
        state.PushValue(1);        /* move function to top */
        state.XMove(newThread, 1); /* move function from L to NL */
        return 1;
    }
    private static int _Resume(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTHREAD);
        LuaState thread  = state.ToThread(1);
        bool     success = _ResumeHelper(state, thread, (short)(state.TopLdx - 1), out int resultCount);
        if (!success) {
            state.Push(false);
            state.Insert(-2);
            return 2; /* return false, errorMessage */
        } else {
            state.Push(true);
            state.Insert(-(resultCount + 1));
            return resultCount + 1; /* return true, ... */
        }
    }
    private static int _Running(this LuaState state) {
        /* 返回当前正在运行的线程，以及是否是主线程 */
        bool isMainThread = state.PushSelf();
        state.Push(isMainThread);
        return 2;
    }
    private static int _Status(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTHREAD);
        LuaState thread = state.ToThread(1);
        if (state == thread)
            state.Push("running");
        else {
            switch (thread.ThreadStatus) {
            case ThreadStatus.YIELD:
                state.Push("suspended");
                break;
            case ThreadStatus.OK: {
                if (thread.CallLevel > 0) /* does it have frames? */
                    state.Push("normal"); /* it is running */
                else if (thread.TopLdx == 0)
                    state.Push("dead");
                else
                    state.Push("suspended"); /* initial state */
                break;
            }
            default: /* some error occurred */
                state.Push("dead");
                break;
            }
        }
        return 1;
    }
    private static int _Wrap(this LuaState state) {
        /* 为一个函数创建线程，然后返回一个 wrap 函数，该 wrap 函数每次调用就会尝试 resume 线程 */
        static int Wrapper(LuaState state_) {
            LuaState thread = state_.ToThread(LuaConst.UpvalueLdx(1));
            bool success = _ResumeHelper(state_, thread, (short)state_.TopLdx, out int resultCount);
            if (!success) {
                if (state_.GetType(-1) == LuaConst.TSTRING) { /* error object is a string? */
                    string msg = state_.ToString(-1, out _);
                    state_.Pop();
                    state_.PushTraceback(msg: msg);
                }
                state_.Error();
            }
            return resultCount;
        }
        state._Create();
        state.Push(Wrapper, 1); /* wrapper func */
        return 1;
    }
    private static int _Yield(this LuaState state) {
        return state.Yield((short)state.TopLdx);
    }
    private static int _Yieldable(this LuaState state) {
        state.Push(state.Yieldable);
        return 1;
    }

    /* ---------------- Utils ---------------- */

    private static bool _ResumeHelper(LuaState from, LuaState thread, short argCount, out int resultCount) {
        /* 返回 false 时，from 上会有一个报错信息；返回 true 时，from 上有 thread 的返回值（yield 或 return） */
        resultCount = 0;
        if (!thread.LuaCheckStack(argCount)) {
            from.Push($"too many arguments to resume: {argCount}");
            return false;
        }
        if (thread.ThreadStatus == ThreadStatus.OK && thread.TopLdx == 0) {
            from.Push("cannot resume dead coroutine"); /* 协程上没有可执行代码 */
            return false;
        }
        from.XMove(thread, argCount);
        ThreadStatus threadStatus = thread.Resume(from, argCount);
        if (threadStatus == ThreadStatus.OK || threadStatus == ThreadStatus.YIELD) {
            resultCount = thread.TopLdx;
            if (!from.LuaCheckStack(resultCount + 1)) {
                thread.Pop(resultCount); /* remove results anyway */
                from.Push($"too many results to resume: {resultCount}");
                return false;
            }
            thread.XMove(from, resultCount); /* move yielded values */
            return true;
        } else {
            thread.XMove(from, 1); /* move error message */
            if (from.GetType(-1) == LuaConst.TSTRING) {
                from.Push("\n");
                from.Push(thread.Traceback(endReturn:false));
                from.LuaConcat(3);
            }
            return false;
        }
    }
}

}
