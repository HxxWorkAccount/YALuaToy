namespace YALuaToy.Tests.Utils {

using System.Diagnostics;
using YALuaToy.Core;
using YALuaToy.Const;
using Xunit;
using Xunit.Sdk;
using System.Runtime.CompilerServices;

internal static class CommonTestUtils
{
    public static readonly object                  NIL      = new object();
    public static readonly TextWriterTraceListener listener = new TextWriterTraceListener(Console.Out);

    public static string CWD() {
        if (Environment.GetEnvironmentVariable("XUNIT_TEST") == "true")
            return Path.GetFullPath("../../../../");
        else
            return Path.GetFullPath(".");
    }
    public static string GetPath(string path) {
        return Path.Join(CWD(), path);
    }

    public static LuaValue CreateLuaValue(object obj) {
        if (obj == NIL) {
            return LuaValue.NIL;
        } else if (obj is bool b) {
            return b ? LuaValue.TRUE : LuaValue.FALSE;
        } else if (obj is int i) {
            return new LuaValue(i);
        } else if (obj is long li) {
            return new LuaValue(li);
        } else if (obj is double d) {
            return new LuaValue(d);
        } else if (obj is IntPtr l) {
            return new LuaValue(l);
        } else if (obj is string s) {
            return new LuaValue(s);
        } else if (obj is LuaObject lobj) {
            return new LuaValue(lobj);
        } else if (obj is LuaCFunction func) {
            return new LuaValue(func);
        }
        throw new XunitException($"Unsupported type: {obj.GetType()}");
    }

    public static void InitTest() {
        /* redirect listener */
        Trace.Listeners.Remove(listener);
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        /* mark for test */
        Environment.SetEnvironmentVariable("XUNIT_TEST", "true");
    }

    public static int CommonCoroCFunction(LuaState state) {
        double a = state.ToNumber(1, out bool ok1);
        double b = state.ToNumber(2, out bool ok2);
        if (!ok1 || !ok2)
            state.Yield(0, IntPtr.Zero, CommonKFunction);
        state.Pop(2);
        state.Push(a * b);
        return 1;
    }

    public static int CommonKFunction(LuaState state, ThreadStatus status, IntPtr ctx) {
        state.Push(1001);
        return 1;
    }

    public static LuaCFunction CreateCFunc(CreateCFuncConfig config) {
        return CreateCFunc(
            config.counter, config.thread, config.errorFunc, config.call, config.pcall, config.cont, config.throwError, config.errorMsg,
            config.listener, config.myFunc
        );
    }

    public static LuaCFunction CreateCFunc(
        Counter counter,
        LuaState? thread                     = null,
        LuaCFunction? errorFunc              = null,
        LuaCFunction? call                   = null,
        bool pcall                           = true,
        LuaKFunction? cont                   = null,
        bool   throwError                    = false,
        string errorMsg                      = "",
        Action<LuaState, LuaState>? listener = null,
        LuaCFunction? myFunc                 = null
    ) {
        if (myFunc != null)
            return myFunc;
        return state => {
            LuaState callThread = thread ?? state;
            counter++;
            errorMsg = string.IsNullOrEmpty(errorMsg) ? "Error: CreateCFunc" : errorMsg;
            if (call != null) {
                if (errorFunc != null)
                    callThread.Push(errorFunc);
                callThread.Push(call);
                if (state != callThread)
                    callThread.Resume(state, 0);
                else if (pcall)
                    callThread.PCall(0, 0, -2, 0, cont);
                else
                    callThread.Call(0, 0, 0, cont);
            }
            if (listener != null)
                listener(state, callThread);
            if (throwError)
                throw new LuaRuntimeError(errorMsg);
            return 0;
        };
    }

    public static LuaCFunction CreateErrorFunc(Counter counter, bool markString = false) {
        return state => {
            counter++;
            if (markString) {
                string msg = state.ToString(-1, out bool success);
                Assert.True(success, "This error func can only handle string error object.");
                state.Pop();
                state.Push($"{msg}^"); /* add mark */
            }
            return 1;
        };
    }

    public static LuaKFunction CreateKFunc(Counter counter) {
        return (state, status, ctx) => {
            counter++;
            return 0;
        };
    }

    public static int InvalidCFunc(LuaState _) {
        throw new LuaRuntimeError("Error: ThrowErrorFunc");
    }

    public static int InvalidErrorFunc(LuaState _) {
        Assert.Fail("This error func expected not to be called.");
        return 0;
    }

    public static void SuperGC() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

internal class Counter
{
    private int _count;
    public int  Count => _count;

    public Counter(): this(0) { }
    public Counter(int count) {
        _count = count;
    }

    public static Counter operator +(Counter counter, int offset) {
        return new Counter(counter._count + offset);
    }
    public static Counter operator -(Counter counter, int offset) {
        return new Counter(counter._count - offset);
    }
    public static Counter operator ++(Counter counter) {
        counter._count++;
        return counter;
    }
    public static Counter operator --(Counter counter) {
        counter._count--;
        return counter;
    }
}

internal class CreateCFuncConfig
{
    public string   name;
    public Counter  counter;
    public LuaState thread;
    public LuaCFunction? errorFunc;
    public LuaCFunction? call;
    public bool pcall;
    public LuaKFunction? cont;
    public bool   throwError;
    public string errorMsg;
    public Action<LuaState, LuaState>? listener;
    public LuaCFunction? myFunc;

    public CreateCFuncConfig(
        string   name,
        Counter  counter,
        LuaState thread,
        LuaCFunction? errorFunc              = null,
        LuaCFunction? call                   = null,
        bool pcall                           = true,
        LuaKFunction? cont                   = null,
        bool throwError                      = false,
        Action<LuaState, LuaState>? listener = null,
        LuaCFunction? myFunc                 = null
    ) {
        this.name       = name;
        this.counter    = counter;
        this.thread     = thread;
        this.errorFunc  = errorFunc;
        this.call       = call;
        this.pcall      = pcall;
        this.cont       = cont;
        this.throwError = throwError;
        this.listener   = listener;
        errorMsg        = $"Error: {name}";
        this.myFunc     = myFunc;
    }
}

}
