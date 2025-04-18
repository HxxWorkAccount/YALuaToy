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

public static class LuaStandardLib
{
    internal static readonly string LUA_VERSION_SUFFIX = $"_{LuaConst.VERSION_MAJOR}_{LuaConst.VERSION_MINOR}";
    internal const string           LUA_PATH_VAR       = "LUA_PATH";
    internal const string           LUA_CPATH_VAR      = "LUA_CPATH";

    private static readonly List<(string, LuaCFunction)> stds = [
        ("_G", LuaBaseLib.OpenLib_Base),
        (LuaLibLoader.LIB_NAME, LuaLibLoader.OpenLib_Package),
        (LuaCoroutineLib.LIB_NAME, LuaCoroutineLib.OpenLib_Coroutine),
        (LuaTableLib.LIB_NAME, LuaTableLib.OpenLib_Table),
        (LuaMathLib.LIB_NAME, LuaMathLib.OpenLib_Math),
    ];

    public static void OpenSTD(this LuaState state) {
        foreach (var (name, func) in stds) {
            state.SimplifiedRequire(name, func, true);
            state.Pop();
        }
    }
}

}
