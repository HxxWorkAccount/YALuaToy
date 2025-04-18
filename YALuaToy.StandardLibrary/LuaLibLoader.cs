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

public static class LuaLibLoader
{
    public const string LIB_NAME     = "package";
    public const string NOENV_CONFIG = "LUA_NOENV"; /* 是否不使用环境变量 */
    public const string LUA_IGMARK   = "-";

    private static readonly List<(string, LuaCFunction)> packageFuncs = [
        ("loadlib", _LoadLib),
        ("searchpath", _SearchPath),
    ];
    private static readonly List<(string, LuaCFunction)> requireFuncs = [
        ("require", _Require),
    ];
    private static readonly List<LuaCFunction> searcherFuncs = [
        _SearchPreloadLib,
        _SearchLuaFile,
    ];

    internal static int OpenLib_Package(this LuaState state) {
        state.NewLib(packageFuncs); /* create 'package' table */

        /* 创建 searchers 子表，该表是一个数组，里有查找函数；顺序即优先级，会按顺序调用直到加载出模块 */
        state.NewTable(); /* searchers 表 */
        for (int i = 0; i < searcherFuncs.Count; i++) {
            state.PushValue(-2); /* -2 位置上是 package 表，要作为 searcher 的上值 */
            state.Push(searcherFuncs[i], 1);
            state.RawSetTable(-2, i + 1); /* searchers[i] = searcherFuncs[i] */
        }
        state.SetTable(-2, "searchers"); /* package.searchers = searchers */

        /* 设置其他字段 preload/path/cpath/loaded */
        state.Push(state._GetLuaPath(LuaStandardLib.LUA_PATH_VAR, LuaConfig.LUA_PATH_DEFAULT));
        state.SetTable(-2, "path");
        state.Push(""); /* 暂不支持加载动态连接库，但还是提供 cpath 这个变量吧 */
        state.SetTable(-2, "cpath");
        state.Push(  //
            $"{LuaConfig.LUA_DIRSEP}\n{LuaConfig.LUA_PATH_SEP}\n{LuaConfig.LUA_PATH_MARK}\n{LuaConfig.LUA_EXEC_DIR}\n{LUA_IGMARK}\n"
        );
        state.SetTable(-2, "config");
        state.GetSubTable(LuaConst.REGISTRYINDEX, LuaAuxiliaryLib.LOADED_TABLE_KEY);
        state.SetTable(-2, "loaded");
        state.GetSubTable(LuaConst.REGISTRYINDEX, LuaAuxiliaryLib.PRELOAD_TABLE_KEY);
        state.SetTable(-2, "preload");

        /* 设置 require 函数 */
        state.PushGlobalTable();         /* _ENV */
        state.PushValue(-2);             /* copy: package */
        state.SetFuncs(requireFuncs, 1); /* _ENV["require"] = _Require，以 package 为上值 */
        state.Pop();                     /* pop _ENV，留下 package 在栈上 */
        return 1;
    }

    private static int _Require(this LuaState state) {
        string moduleName = state.GetStringArg(1);

        void PushFileLoader(string moduleName_) {
            if (state.GetTable(LuaConst.UpvalueLdx(1), "searchers") != LuaConst.TTABLE)
                throw new LuaRuntimeError("'package.searchers' must be a table");
            StringBuilder errorMsgBuilder = new();
            /* 按顺序遍历查找器 */
            for (int i = 1;; i++) {
                if (state.RawGetTable(-1, i) == LuaConst.TNIL) { /* push searchers[i]，若返回 nil 表示查找完所有遍历器 */
                    state.Pop();                                 /* pop nil */
                    throw new LuaRuntimeError($"module '{moduleName_}' not found: {errorMsgBuilder}.");
                }
                state.Push(moduleName_);
                state.Call(1, 2); /* loader, ud = searchers[i](moduleName) */
                if (state.IsFunction(-2))
                    return;
                else if (state.IsString(-2)) {
                    state.Pop(); /* pop extra return */
                    errorMsgBuilder.AppendLine(state.ToString(-1, out _));
                    state.Pop(); /* pop error msg */
                } else
                    state.Pop(2);
            }
        }

        /* 尝试读取缓存 */
        state.TopLdx = 1; /* 只需要一个参数 */
        state.GetTable(LuaConst.REGISTRYINDEX, LuaAuxiliaryLib.LOADED_TABLE_KEY);
        state.GetTable(2, moduleName); /* LOADED[moduleName] */
        if (state.ToBoolean(-1))
            return 1;

        /* 尝试加载库 */
        state.Pop();                /* 弹出 LOADED[moduleName] 的结果 */
        PushFileLoader(moduleName); /* loader, ud */
        state.Push(moduleName);     /* loader, ud, moduleName */
        state.Insert(-2);           /* loader, moduleName, ud */
        state.Call(2, 1);           /* loader(moduleName, ud) */
        if (!state.IsNil(-1))
            state.SetTable(2, moduleName); /* LOADED[moduleName] = returned value */
        if (state.GetTable(2, moduleName) == LuaConst.TNIL) {
            state.Push(false);
            state.PushValue(-1);
            state.SetTable(2, moduleName); /* LOADED[moduleName] = false */
        }
        return 1; /* 栈上剩余一个布尔值，表示是否成功 */
    }

    private static int _LoadLib(this LuaState state) {
        string init = state.GetStringArg(2);
        state.PushNil();
        state.Push("dynamic libraries disabled");
        state.Push(init);
        return 3;
    }
    private static int _SearchPath(this LuaState state) {
        /* 查找 lua 脚本 */
        bool success = state._SearchPathHelper(
            state.GetStringArg(1), state.GetStringArg(2), state.GetStringArg(3, "."), state.GetStringArg(4, LuaConfig.LUA_DIRSEP),
            out string result
        );
        if (success) {
            state.Push(result);
            return 1;
        } else {
            state.PushNil();
            state.Push(result); /* result 是 error message */
            return 2;           /* return nil + error message */
        }
    }

    private static int _SearchPreloadLib(this LuaState state) {
        /* 搜索 package.preload 表，通常来说在宿主侧注册的模块会放到这个表 */
        string moduleName = state.GetStringArg(1);
        state.GetSubTable(LuaConst.REGISTRYINDEX, LuaAuxiliaryLib.PRELOAD_TABLE_KEY);
        if (state.GetTable(-1, moduleName) == LuaConst.TNIL) { /* not found? */
            state.Pop();                                       /* pop nil */
            state.Push($"module '{moduleName}' not found in package.preload");
        }
        return 1;
    }
    private static int _SearchLuaFile(this LuaState state) {
        string moduleName = state.GetStringArg(1);
        bool   success    = state._FindFile(moduleName, "path", LuaConfig.LUA_DIRSEP, out string result);
        if (!success) {
            state.Push(result); /* 压入错误信息 */
            return 1;
        }
        ThreadStatus threadStatus = state.LoadFile(result);
        if (threadStatus == ThreadStatus.OK) {
            state.Push(result); /* 压入 filepath */
            return 2; /* closure, filepath。之后会被调用 closure(filepath)，模块需要自主 return 一个 table 到栈上 */
        } 
        /* 注：此时栈顶是 LoadFile 的报错信息 */
        throw new LuaRuntimeError($"error loading module '{moduleName}' from file '{result}':\n\t{state.ToString(-1, out _)}");
    }

    /* ---------------- Utils ---------------- */

    /* 获得 path 变量，返回字符串（CLua 这里还有注册 package 字段的功能，但因为违反单一职责原则，所以不搞，改为返回字符串） */
    private static string _GetLuaPath(this LuaState state, string envname, string default_) {
        string env          = $"envname{LuaStandardLib.LUA_VERSION_SUFFIX}";
        string executionDir = AppDomain.CurrentDomain.BaseDirectory;
        string? path        = Environment.GetEnvironmentVariable(env);
        if (path == null) {
            env  = envname;
            path = Environment.GetEnvironmentVariable(env);
        }
        if (path == null || !state._UseEnv())
            return default_;
        else {
            /* 注意，将 ';;' 展开为 default 的操作只会在初始化时做一次。用户运行时就没法用 ';;' 表示默认值了 */
            path = path.Replace(
                $"{LuaConfig.LUA_PATH_SEP}{LuaConfig.LUA_PATH_SEP}", $"{LuaConfig.LUA_PATH_SEP}{default_}{LuaConfig.LUA_PATH_SEP}"
            );
            path = path.Replace(LuaConfig.LUA_EXEC_DIR, executionDir); /* CLua 只支持 Windows 这么做，我这里就不管了 */
            return path;
        }
    }
    private static bool _UseEnv(this LuaState state) {
        state.GetTable(LuaConst.REGISTRYINDEX, NOENV_CONFIG);
        bool result = !state.ToBoolean(-1);
        state.Pop();
        return result;
    }

    private static bool _FindFile(this LuaState state, string moduleName, string pathKey, string dirsep, out string result) {
        state.GetTable(LuaConst.UpvalueLdx(1), pathKey); /* package[pathKey]，这里可能是 'path' 或 'cpath'，目前仅支持 'path' */
        string pathPatterns = state.ToString(-1, out bool hasPath);
        if (!hasPath)
            throw new LuaRuntimeError($"'package.{pathKey}' must be a string");
        return state._SearchPathHelper(moduleName, pathPatterns, ".", dirsep, out result);
    }
    private static bool
    _SearchPathHelper(this LuaState state, string moduleName, string pathPatterns, string sep, string dirsep, out string result) {
        StringBuilder sb = new();
        if (!string.IsNullOrEmpty(sep))
            moduleName = moduleName.Replace(sep, dirsep); /* replace it by 'dirsep' */
        string[] pathPatternArray = pathPatterns.Split(LuaConfig.LUA_PATH_SEP);
        foreach (string pathPattern in pathPatternArray) {
            string filepath = pathPattern.Replace("?", moduleName);
            if (File.Exists(filepath)) {
                result = filepath;
                return true;
            }
            sb.AppendLine($"\tno file '{filepath}'");
        }
        result = sb.ToString();
        return false;
    }
}

}
