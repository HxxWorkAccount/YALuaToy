/* 注：该功能本应做进标准库，但由于目前没有实现 ldebug （提供反射调试信息的模块），所以只好放到这里，通过直接调用内核接口来获取部分调试信息 */
namespace YALuaToy {

using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using YALuaToy.Core;
using YALuaToy.Const;

using Tag = System.SByte;
using System.Runtime.InteropServices;

public static class LuaAuxiliaryLib
{
    public const string LOADED_TABLE_KEY  = "_LOADED";
    public const string PRELOAD_TABLE_KEY = "_PRELOAD";

    /* ---------------- Stack and Value Utils ---------------- */

    public static Tag GetMetaField(this LuaState state, int ldx, string fieldName) {
        if (!state.GetMetatable(ldx))
            return LuaConst.TNIL;
        state.Push(fieldName);
        Tag tag = state.RawGetTable(-2);
        if (tag == LuaConst.TNIL)
            state.Pop(2); /* 移除残留的 metatable 和 fieldname */
        else
            state.Remove(-2); /* 只移除 metatable */
        return tag;
    }
    public static Tag GetRegistryField(this LuaState state, string fieldName) {
        return state.GetMetaField(LuaConst.REGISTRYINDEX, fieldName);
    }
    public static bool CallMetaField(this LuaState state, int ldx, string fieldName) {
        if (state.GetMetaField(ldx, fieldName) == LuaConst.TNIL)
            return false;
        state.PushValue(ldx);
        state.Call(1, 1);
        return true;
    }

    /* 尝试读取 ldx 元素的 fieldName 字段，如果该字段是表就压入栈；如果不是就创建一个，赋值到 fieldName 上，新表也会留在栈上 */
    public static bool GetSubTable(this LuaState state, int ldx, string fieldName) {
        if (state.GetTable(ldx, fieldName) == LuaConst.TTABLE) {
            return true;
        } else {
            state.Pop();
            state.NewTable();
            state.PushValue(-1); /* 下面 SetTable 会消耗掉对应的 table，所以要备份一下 */
            state.SetTable(ldx, fieldName);
            return false;
        }
    }

    /* 相比 ToString 该接口会压入一个字符串而不是改变原有值
       另外，除了 number 外支持更多类型，还会优先检测 __tostring, __name 元字段 */
    public static string GetString(this LuaState state, int ldx) {
        if (state.CallMetaField(ldx, "__tostring")) {
            if (!state.IsString(-1))
                throw new LuaException("'__tostring' must return a string");
        } else {
            /* 其实 LuaObject 里有一个 ToString 比这个更强，但考虑到封装性还是不调用了 */
            switch (state.GetType(ldx)) {
            case LuaConst.TNUMBER:
            case LuaConst.TSTRING:
                state.PushValue(ldx);
                break;
            case LuaConst.TBOOLEAN:
                state.Push(state.ToBoolean(ldx) ? "true" : "false");
                break;
            case LuaConst.TNIL:
                state.Push("nil");
                break;
            default: {
                Tag    tag  = state.GetMetaField(ldx, "__name");
                string type = tag == LuaConst.TSTRING ? state.ToString(-1, out _) : state.TypeNameAt(ldx);
                state.Push("%s: %s", type, CommonUtils.ToString(checked((IntPtr)state.ToId(ldx))));
                if (tag != LuaConst.TNIL)
                    state.Remove(-2); /* 移除 '__name' */
                break;
            }
            }
        }
        return state.ToString(-1, out _);
    }
    public static string TypeNameAt(this LuaState state, int ldx) {
        return LuaState.TypeName(state.GetType(ldx));
    }

    public static string GetValueString(this LuaState state, int ldx)  {
        /* 这个是我自己加的，用来获取 LuaValue.ToString 的信息，这个在打印表时非常好用 */
        LuaValue value = state._Get(ldx);
        if (value.IsString)
            return value.Str;
        return value.ToString();
    }

    public static long LengthAt(this LuaState state, int ldx) {
        state.LuaGetLength(ldx);
        long result = state.ToInteger(-1, out bool success);
        if (!success)
            throw new LuaRuntimeError($"Object length is not an integer, ldx: {ldx}");
        state.Pop();
        return result;
    }

    /* ---------------- Arg ---------------- */

    public static double GetNumberArg(this LuaState state, int argLdx) {
        double result = state.ToNumber(argLdx, out bool success);
        if (!success)
            throw new LuaArgTypeError(state, argLdx, LuaConst.TNUMBER);
        return result;
    }
    public static double GetNumberArg(this LuaState state, int argLdx, double default_) {
        double result = state.ToNumber(argLdx, out bool success);
        if (!success)
            return default_;
        return result;
    }
    public static long GetIntegerArg(this LuaState state, int argLdx) {
        long result = state.ToInteger(argLdx, out bool success);
        if (!success)
            throw new LuaArgTypeError(state, argLdx, LuaConst.TNUMINT);
        return result;
    }
    public static long GetIntegerArg(this LuaState state, int argLdx, long default_) {
        long result = state.ToInteger(argLdx, out bool success);
        if (!success)
            return default_;
        return result;
    }
    public static string GetStringArg(this LuaState state, int argLdx) {
        string result = state.ToString(argLdx, out bool success);
        if (!success)
            throw new LuaArgTypeError(state, argLdx, LuaConst.TSTRING);
        return result;
    }
    public static string GetStringArg(this LuaState state, int argLdx, string default_) {
        string result = state.ToString(argLdx, out bool success);
        if (!success)
            return default_;
        return result;
    }

    public static void CheckArgType(this LuaState state, int argLdx, params sbyte[] expectedTypes) {
        sbyte type = state.GetType(argLdx);
        if (expectedTypes.Any(t => t == type))
            return;
        throw new LuaArgTypeError(state, argLdx, string.Join(",", expectedTypes.Select(t => LuaState.TypeName(t)).ToArray()));
    }
    public static void CheckArgType(this LuaState state, int argLdx, sbyte expectedType) {
        if (state.GetType(argLdx) != expectedType)
            throw new LuaArgTypeError(state, argLdx, expectedType);
    }
    public static void CheckAnyArg(this LuaState state, int argLdx) {
        if (state.GetType(argLdx) == LuaConst.TNONE)
            throw new LuaArgError(state, argLdx, $"expected arg at {argLdx} pos");
    }
    public static void CheckNotNilArg(this LuaState state, int argLdx) {
        state.CheckAnyArg(argLdx);
        if (state.GetType(argLdx) == LuaConst.TNIL)
            throw new LuaArgError(state, argLdx, $"expected not nil arg at {argLdx} pos");
    }

    /* ---------------- Registry ---------------- */

    /* 在注册表上创建一个 name->table 的映射，该 table 会在其他地方用于元表。该操作是为了保证元表唯一 */
    public static bool NewMetatable(this LuaState state, string name) {
        if (state.GetRegistryField(name) != LuaConst.TNIL)
            return false; /* 该名字已存在 */
        state.Pop();
        state.NewTable();
        state.Push(name);
        state.SetTable(-2, "__name");                 /* metatable.__name = tname */
        state.PushValue(-1);                          /* 保证栈上会留下新建的表 */
        state.SetTable(LuaConst.REGISTRYINDEX, name); /* registry.name = metatable */
        return true;
    }
    /* 调用前需向栈压入一个表 a，然后从注册表上取下 name 对应的表 b，把表 b 设为表 a 的元表 */
    public static void SetMetatable(this LuaState state, string name) {
        state.GetRegistryField(name);
        state.SetMetatable(-2);
    }

    /* ---------------- Load File ---------------- */

    public static ThreadStatus LoadFile(this LuaState state, string filepath) {
        if (string.IsNullOrEmpty(filepath)) { /* 读取标准输入 */
            using (TextReader reader = Console.In) {
                return state.Load(reader, "=stdin");
            }
        } else {
            using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read)) {
                using (TextReader reader = new StreamReader(fs)) {
                    return state.Load(reader, filepath);
                }
            }
        }
    }
    public static ThreadStatus LoadString(this LuaState state, string chunk, string chunkname = null) {
        using (StringReader reader = new StringReader(chunk)) {
            chunkname = string.IsNullOrEmpty(chunkname) ? "chunk" : chunkname;
            return state.Load(reader, chunkname);
        }
    }
    public static ThreadStatus DoFile(this LuaState state, string filepath) {
        ThreadStatus threadStatus = state.LoadFile(filepath);
        if (threadStatus == ThreadStatus.OK)
            return state.PCall(0, LuaConst.MULTRET, 0);
        return threadStatus;
    }
    public static ThreadStatus DoString(this LuaState state, string chunk) {
        ThreadStatus threadStatus = state.LoadString(chunk);
        if (threadStatus == ThreadStatus.OK)
            return state.PCall(0, LuaConst.MULTRET, 0);
        return threadStatus;
    }

    /* ---------------- Library Utils ---------------- */

    public static void NewLib(this LuaState state, IList<(string, LuaCFunction)> funcs) {
        state.NewTable();
        state.SetFuncs(funcs, 0);
    }
    public static void SetFuncs(this LuaState state, IList<(string, LuaCFunction)> funcs, int upvalueCount) {
        /* 调用前栈结构是：table + n 个 upvalue */
        state.ForcedCheckStack(upvalueCount, $"too many upvalue.");
        foreach (var (name, func) in funcs) {
            for (int i = 0; i < upvalueCount; i++) /* 把上值复制一份，对所有 func 都设置相同的上值 */
                state.PushValue(-upvalueCount);
            state.Push(func, upvalueCount);
            state.SetTable(-(upvalueCount + 2), name);
        }
        state.Pop(upvalueCount);
    }

    public static void SimplifiedRequire(this LuaState state, string moduleName, LuaCFunction pushModule, bool global) {
        state.GetSubTable(LuaConst.REGISTRYINDEX, LOADED_TABLE_KEY);
        state.GetTable(-1, moduleName); /* LOADED[modname] */
        if (!state.ToBoolean(-1)) {     /* module not loaded */
            state.Pop();                /* remove field */
            state.Push(pushModule);
            state.Push(moduleName);         /* moduleName 是 pushModule 的参数 */
            state.Call(1, 1);               /* 预期返回一个 LuaTable 表示模块 */
            state.PushValue(-1);            /* 备份模块 */
            state.SetTable(-3, moduleName); /* LOADED[modname] = module */
        }
        state.Remove(-2); /* 移除 LOADED 表，留下模块在栈顶 */
        if (global) {
            state.PushValue(-1);         /* copy of module */
            state.SetGlobal(moduleName); /* _G[modname] = module */
        }
    }

    /* ---------------- Other Utils ---------------- */

    public static void ForcedCheckStack(this LuaState state, int n, string failedMsg = "") {
        if (state.LuaCheckStack(n))
            return;
        if (string.IsNullOrEmpty(failedMsg))
            throw new LuaRuntimeError($"Check stack failed ({n}), stack overflow.");
        else
            throw new LuaRuntimeError($"Check stack failed ({n}), stack overflow. Hint: {failedMsg}");
    }

    public static void PushTraceback(this LuaState state, int level = 1, string msg = "") {
        state.PushTraceback(state, level, msg);
    }
    public static void PushTraceback(this LuaState state, LuaState thread, int level = 1, string msg = "") {
        state.Push(thread.Traceback(level, msg));
    }
    public static string Traceback(this LuaState state, int level = 1, string msg = "", bool endReturn=true) {
        /* 只打印 Lua 调用栈信息 */
        StringBuilder sb = new StringBuilder();
        if (!string.IsNullOrEmpty(msg))
            sb.AppendLine(msg);

        int      currLevel = 0;
        CallInfo ci        = state._vmCI;
        for (; ci != null; ci = ci.prev) {
            if (!ci.IsLua)
                continue;
            if (++currLevel < level)
                continue;
            LClosure lclosure = ci.LClosure;
            LuaProto proto    = lclosure.proto;
            sb.AppendLine($"    {proto.source.Str}, line {proto.Lines[ci.PC-1]}");
        }
        if (!endReturn)
            sb.Remove(sb.Length - 1, 1); /* remove last \n */
        return sb.ToString();
    }
}

/* ================== 一些异常类 ================== */

public class LuaArgError : LuaRuntimeError
{
    public LuaArgError(LuaState thread, int argLdx, string msg): base(GetMsg(thread, argLdx, msg)) { }

    internal static string GetMsg(LuaState thread, int argLdx, string msg) {
        return $"bad argument #{argLdx} ({msg})";
    }
}

public class LuaArgTypeError : LuaRuntimeError
{
    public LuaArgTypeError(LuaState thread, int argLdx, sbyte expectedType): base(GetMsg(thread, argLdx, expectedType)) { }
    public LuaArgTypeError(LuaState thread, int argLdx, string expected): base(GetMsg(thread, argLdx, expected)) { }

    internal static string GetMsg(LuaState thread, int argLdx, string expected) {
        if (thread.GetMetaField(argLdx, "__name") == LuaConst.TSTRING) {
            string typeName = thread.GetString(-1);
            thread.Pop();
            return $"bad argument #{argLdx} (expect '{expected}', but got '{typeName}')";
        } else {
            string typeName = thread.TypeNameAt(argLdx);
            return $"bad argument #{argLdx} (expect '{expected}', but got '{typeName}')";
        }
    }
    internal static string GetMsg(LuaState thread, int argLdx, sbyte expectedType) {
        string exptedTypeName = LuaState.TypeName(expectedType);
        return GetMsg(thread, argLdx, exptedTypeName);
    }
}

}
