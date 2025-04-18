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

internal static class LuaBaseLib
{
    public const string METATABLE_PROTECTED_KEY = "__metatable";

    private static readonly List<(string, LuaCFunction)> baseFuncs = [
        ("assert", _Assert),
        ("collectgarbage", _CollectGarbage),
        ("dofile", _DoFile),
        ("error", _Error),
        ("getmetatable", _GetMetatable),
        ("ipairs", _IPairs),
        ("loadfile", _LoadFile),
        ("load", _Load),
        ("next", _Next),
        ("pairs", _Pairs),
        ("pcall", _PCall),
        ("print", _Print),
        ("rawequal", _RawEqual),
        ("rawlen", _RawLength),
        ("rawget", _RawGet),
        ("rawset", _RawSet),
        ("select", _Select),
        ("setmetatable", _SetMetatable),
        ("tonumber", _ToNumber),
        ("tostring", _ToString),
        ("type", _Type),
        ("xpcall", _XPCall),
    ];

    internal static int OpenLib_Base(this LuaState state) {
        /* 打开标准库函数 */
        state.PushGlobalTable();
        state.SetFuncs(baseFuncs, 0);
        /* set global _G */
        state.PushValue(-1);
        state.SetTable(-2, "_G");
        /* set global _VERSION */
        state.Push(LuaConst.VERSION);
        state.SetTable(-2, "_VERSION");
        return 1;
    }

    /* ---------------- Base Funcs ---------------- */

    private static int _Assert(this LuaState state) {
        if (state.ToBoolean(1))
            return state.TopLdx; /* assert 为 true 直接返回所有参数 */
        else {
            state.CheckAnyArg(1);
            state.Remove(1);
            string msg = "Assertion failed!";
            if (state.IsString(1)) /* 如果用户有提供报错信息，则用用户提供的 */
                msg = state.ToString(1, out _);
            state.TopLdx = 0; /* 清空参数 */
            throw new LuaRuntimeError($"{msg}\n{state.Traceback()}");
        }
    }
    private static int _CollectGarbage(this LuaState state) {
        if (state.TopLdx == 0 || state.GetStringArg(1) == "collect") {
            GC.Collect();
            return 0;
        } else if (state.GetStringArg(1) == "count") {
            long   bytes = GC.GetTotalMemory(false);
            double kb    = bytes / 1024.0;
            state.Push(kb);
            return 1;
        }
        state.Push(0); /* 其他情况无脑返回 0 */
        return 1;
    }
    private static int _DoFile(this LuaState state) {
        static int ContinueDoFile(LuaState state_, ThreadStatus status, IntPtr ctx) {
            return state_.TopLdx - 1; /* 返回 lua 文件的所有返回值，-1 是为了去掉一开始的 filepath */
        }
        string filepath = state.GetStringArg(1);
        state.ClearFrame();
        if (state.LoadFile(filepath) != ThreadStatus.OK)
            throw new LuaRuntimeError($"Load file failed: {filepath}.");
        state.Call(0, LuaConst.MULTRET, 0, ContinueDoFile); /* 一旦 Call 了就要考虑连续性问题 */
        return ContinueDoFile(state, 0, 0);
    }
    private static int _Error(this LuaState state) {
        int level = (int)state.GetIntegerArg(2, 1);
        if (state.IsString(1) || state.IsNil(1)) {
            string msg = state.IsNil(1) ? "" : state.ToString(1, out _);
            if (level > 0)
                msg = state.Traceback(level, msg);
            state.ClearFrame();
            throw new LuaRuntimeError(msg);
        } else {
            state.Error();
        }
        return 0;
    }
    private static int _GetMetatable(this LuaState state) {
        state.CheckAnyArg(1);
        if (!state.GetMetatable(1)) {
            state.PushNil();
            return 1;
        }
        state.GetMetaField(1, METATABLE_PROTECTED_KEY); /* 尝试通过保护字段来读取 metatable */
        return 1;
    }
    private static int _IPairs(this LuaState state) { /* ipairs 迭代器生成器 */
        static int IPairsIterator(LuaState state_) {
            long i = state_.GetIntegerArg(2) + 1;
            state_.Push(i);
            /* 执行 t[i]，迭代器返回 nil 表示已到终点；否则返回 (i, t[i]) */
            return state_.GetTable(1, i) == LuaConst.TNIL ? 1 : 2;
        }
        state.CheckAnyArg(1);
        state.Push(IPairsIterator); /* 泛型 for 迭代器 */
        state.PushValue(1);         /* 泛型 for 循环常量 */
        state.Push(0);              /* 泛型 for 循环变量 */
        return 3;
    }
    private static int _LoadFile(this LuaState state) {
        string       filepath     = state.GetStringArg(1, "");
        string       mode         = state.GetStringArg(2, "");
        int          envLdx       = state.IsNone(3) ? 0 : 3;
        ThreadStatus threadStatus = state.LoadFile(filepath);
        return state._HandleLoadResult(threadStatus, envLdx);
    }
    private static int _Load(this LuaState state) {
        string       chunk  = state.ToString(1, out bool success);
        string       mode   = state.GetStringArg(3, "");
        int          envLdx = state.IsNone(4) ? 0 : 4;
        ThreadStatus threadStatus;
        if (success) {
            string chunkname = state.GetStringArg(2, "chunk");
            threadStatus     = state.LoadString(chunk, chunkname);
        } else { /* loading from a reader function */
            string chunkname = state.GetStringArg(2, "=(load)");
            state.CheckArgType(1, LuaConst.TFUNCTION);
            threadStatus = state.Load(new ChunkReader(state), chunkname);
        }
        return state._HandleLoadResult(threadStatus, envLdx);
    }
    private static int _Next(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTABLE);
        state.TopLdx = 2;
        if (state.Next(1))
            return 2;
        else {
            state.PushNil();
            return 1;
        }
    }
    private static int _Pairs(this LuaState state) { /* pairs 迭代器生成器 */
        state.CheckAnyArg(1);
        if (state.GetMetaField(1, "__pairs") != LuaConst.TNIL) {
            state.PushValue(1); /* self */
            state.Call(1, 3);   /* iter, const, var = __pairs(self) */
        } else {
            /* 使用 Next 实现 pairs */
            state.Push(_Next);
            state.PushValue(1);
            state.PushNil();
        }
        return 3;
    }
    private static int _PCall(this LuaState state) {
        state.CheckAnyArg(1);
        state.Push(true); /* [pfunc, ..., true] */
        state.Insert(1);  /* [true, pfunc, ...] */
        ThreadStatus threadStatus = state.PCall((short)(state.TopLdx - 2), LuaConst.MULTRET, 0, 0, _ContinuePCall);
        return _ContinuePCall(state, threadStatus, 0);
    }
    private static int _Print(this LuaState state) {
        StringBuilder sb       = new StringBuilder();
        int           argCount = state.TopLdx;
        state.GetGlobal("tostring");
        for (int i = 1; i <= argCount; i++) {
            state.PushValue(-1); /* copy tostring */
            state.PushValue(i);  /* copy args[i] */
            state.Call(1, 1);    /* str = tostring(args[i]) */
            string str = state.ToString(-1, out bool success);
            if (success)
                sb.Append(str).Append(' ');
            else
                throw new LuaRuntimeError($"'tostring' must return a string to 'print'.");
            state.Pop();
        }
        Console.WriteLine(sb.ToString());
        return 0;
    }
    private static int _RawEqual(this LuaState state) {
        state.CheckAnyArg(1);
        state.CheckAnyArg(2);
        state.Push(state.RawEquals(1, 2));
        return 1;
    }
    private static int _RawLength(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTABLE, LuaConst.TSTRING);
        state.Push(state.RawLength(1));
        return 1;
    }
    private static int _RawGet(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTABLE);
        state.CheckAnyArg(2);
        state.TopLdx = 2;
        state.RawGetTable(1);
        return 1;
    }
    private static int _RawSet(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTABLE);
        state.CheckAnyArg(2);
        state.CheckAnyArg(3);
        state.TopLdx = 3;
        state.RawSetTable(1);
        return 1;
    }
    private static int _Select(this LuaState state) {
        int    argCount = state.TopLdx;
        string first    = state.ToString(1, out bool success);
        if (state.GetType(1) == LuaConst.TSTRING && success && first[0] == '#') {
            state.Push(argCount - 1);
            return 1;
        } else {
            long i = state.GetIntegerArg(1);
            if (i < 0)
                i = argCount + i;
            else if (i > argCount)
                i = argCount;
            if (i < 1) /* 注意不能是 0，第一个 n 是指示数量的，不算逻辑参数列表的一部份 */
                throw new LuaRuntimeError($"index out of range: {i}, arg count: {argCount-1}.");
            return argCount - (int)i;
        }
    }
    private static int _SetMetatable(this LuaState state) {
        state.CheckArgType(1, LuaConst.TTABLE);
        state.CheckArgType(2, LuaConst.TNIL, LuaConst.TTABLE);
        if (state.GetMetaField(1, METATABLE_PROTECTED_KEY) != LuaConst.TNIL)
            throw new LuaRuntimeError($"cannot change a protected metatable");
        state.TopLdx = 2;
        state.SetMetatable(1);
        return 1;
    }
    private static int _ToNumber(this LuaState state) {
        if (state.IsNoneOrNil(2)) { /* 单参数格式，直接转换即可 */
            state.CheckAnyArg(1);
            if (state.GetType(1) == LuaConst.TNUMBER) {
                state.TopLdx = 1;
                return 1;
            } else {
                string str = state.ToString(1, out bool success);
                if (success && state.StringToNumber(str) == str.Length)
                    return 1;
            }
        } else { /* 双参数格式，进制转换。这里只支持字符串转整型 */
            state.CheckArgType(1, LuaConst.TSTRING);
            int    base_ = (int)state.GetIntegerArg(2);
            string str   = state.ToString(1, out bool success);
            if (!(2 <= base_ && base_ <= 36))
                throw new LuaRuntimeError($"base out of range: {base_}, should be in [2, 36]");
            if (success && _StringToInteger(str, base_, out long n)) {
                state.Push(n);
                return 1;
            }
        }
        state.PushNil();
        return 1;
    }
    private static int _ToString(this LuaState state) {
        state.CheckAnyArg(1);
        state.GetString(1);
        return 1;
    }
    private static int _Type(this LuaState state) {
        state.CheckAnyArg(1);
        state.Push(state.TypeNameAt(1));
        return 1;
    }
    private static int _XPCall(this LuaState state) {
        /* Do a protected call with error handling. After 'lua_rotate', the
           stack will have <f, err, true, f, [args...]>; so, the function passes
           2 to 'finishpcall' to skip the 2 first values when returning results. */
        int argCount = state.TopLdx;
        state.CheckArgType(2, LuaConst.TFUNCTION); /* [pfunc, errorFunc, ...] */
        state.Push(true);                          /* [pfunc, errorFunc, ..., true] */
        state.PushValue(1);                        /* [pfunc, errorFunc, ..., true, pfunc] */
        state.Rotate(3, 2);                        /* [pfunc, errorFunc, true, pfunc, ...] */
        ThreadStatus threadStatus = state.PCall((short)(argCount - 2), LuaConst.MULTRET, 2, 2, _ContinuePCall);
        return state._ContinuePCall(threadStatus, 2);
    }

    /* ---------------- Utils ---------------- */

    private static bool _StringToInteger(string str, int base_, out long result) {
        result   = 0;
        int  i   = 0;
        bool neg = false;

        void ConsumeWhiteSpace() {
            while (i < str.Length && char.IsWhiteSpace(str[i]))
                i++;
        }

        /* skip initial spaces */
        ConsumeWhiteSpace();
        if (i >= str.Length)
            return false;

        /* handle sign */
        if (str[i] == '-') {
            i++;
            neg = true;
        } else if (str[i] == '+')
            i++;

        if (i >= str.Length || !char.IsLetterOrDigit(str[i])) /* no digit? */
            return false;
        do {
            int digit;
            if (char.IsDigit(str[i]))
                digit = str[i] - '0';
            else
                digit = char.ToUpper(str[i]) - 'A' + 10;
            if (digit >= base_)
                return false; /* invalid numeral */
            result = result * base_ + digit;
            i++;
        } while (i < str.Length && char.IsLetterOrDigit(str[i]));

        ConsumeWhiteSpace();
        result = neg ? -result : result;
        return i == str.Length; /* all characters consumed? */
    }

    private static int _ContinuePCall(this LuaState state, ThreadStatus threadStatus, IntPtr extra) {
        if (threadStatus != ThreadStatus.OK && threadStatus != ThreadStatus.YIELD) { /* error? */
            state.Push(false);
            state.PushValue(-2);
            return 2; /* return false, msg */
        } else        /* 该函数假设调用前已经塞了一个 true 在最顶层，extra 由 PCall 调用时传递，说明有多少元素要忽略 */
            return state.TopLdx - checked((int)extra);
    }

    private static int _HandleLoadResult(this LuaState state, ThreadStatus threadStatus, int envLdx) {
        if (threadStatus == ThreadStatus.OK) {
            if (envLdx != 0) {
                state.PushValue(envLdx);                           /* 创建 env 副本 */
                if (string.IsNullOrEmpty(state.SetUpvalue(-2, 1))) /* 把 lclosure 的第一个上值改为 env，也就是全局环境直接换了 */
                    state.Pop();                                   /* 如果没有 1 号上值。。。那就 pop 掉残留在栈上的 env 副本 */
            }
            return 1;
        } else { /* error (message is on top of the stack) */
            state.PushNil();
            state.Insert(-2);
            return 2; /* nil, errorMsg */
        }
    }
}

internal class ChunkReader : TextReader
{
    private LuaState      _state;
    private StringBuilder _sb;

    public ChunkReader(LuaState state) {
        _state = state;
        _sb    = new StringBuilder();
    }

    public override int Read() { /* 先提供一个能用的实现，后续再完善 */
        if (_sb.Length == 0)
            _DoRead();
        if (_sb.Length > 0) {
            int c = _sb[0];
            _sb.Remove(0, 1);
            return c;
        } else {
            return -1;
        }
    }

    private void _DoRead() {
        _state.ForcedCheckStack(2, "too many nested functions");
        _state.PushValue(1);    /* 备份 reader */
        _state.Call(0, 1);      /* str = reader() */
        if (_state.IsNil(-1)) { /* 没了 */
            _state.Pop();
            return;
        } else if (!_state.IsString(-1))
            throw new LuaRuntimeError("reader function must return a string");
        _sb.Append(_state.ToString(-1, out _));
        _state.Pop();
    }
}

}
