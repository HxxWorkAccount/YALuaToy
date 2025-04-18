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

internal static class LuaMathLib
{
    public const string LIB_NAME = "math";

    private static readonly List<(string, LuaCFunction)> mathFuncs = [
        ("tointeger", _ToInteger),
        ("fmod", _FMod),
        ("max", _Max),
        ("min", _Min),
        ("type", _Type),
    ];

    internal static int OpenLib_Math(this LuaState state) {
        /* 打开数学库 */
        state.NewLib(mathFuncs);

        state.Push(Math.PI);
        state.SetTable(-2, "pi");
        state.Push(double.PositiveInfinity);
        state.SetTable(-2, "huge");
        state.Push(long.MaxValue);
        state.SetTable(-2, "maxinteger");
        state.Push(long.MinValue);
        state.SetTable(-2, "mininteger");

        /* 相关字段 */
        return 1;
    }

    /* ---------------- Lib Funcs ---------------- */

    private static int _ToInteger(this LuaState state) {
        long n = state.ToInteger(1, out bool success);
        if (success)
            state.Push(n);
        else {
            state.CheckAnyArg(1);
            state.PushNil(); /* value is not convertible to integer */
        }
        return 1;
    }
    private static int _FMod(this LuaState state) {
        if (state.IsInteger(1) && state.IsInteger(2)){
            long d = state.ToInteger(2);
            if ((ulong)d + 1 <= 1) { /* special cases: -1 or 0 */
                if (!(d != 0))
                    throw new LuaArgError(state, 2, "zero");
                state.Push(0); /* avoid overflow with 0x80000... / -1 */
            } else
                state.Push(state.ToInteger(1) % d);
        } else
            state.Push(state.GetNumberArg(1) % state.GetNumberArg(2));
        return 1;
    }
    static int _Min(this LuaState state) {
        state.CheckAnyArg(1);        /* at least one arg */
        int argCount = state.TopLdx; /* number of arguments */
        int minLdx   = 1;            /* index of current minimum value */
        for (int i = 2; i <= argCount; i++) {
            if (state.Compare(Comp.LT, i, minLdx))
                minLdx = i;
        }
        state.PushValue(minLdx);
        return 1;
    }
    static int _Max(this LuaState state) {
        state.CheckAnyArg(1);        /* at least one arg */
        int argCount = state.TopLdx; /* number of arguments */
        int maxLdx   = 1;            /* index of current minimum value */
        for (int i = 2; i <= argCount; i++) {
            if (state.Compare(Comp.LT, maxLdx, i))
                maxLdx = i;
        }
        state.PushValue(maxLdx);
        return 1;
    }
    static int _Type(this LuaState state) {
        if (state.GetType(1) == LuaConst.TNUMBER) {
            if (state.IsInteger(1))
                state.Push("integer");
            else
                state.Push("float");
        } else {
            state.CheckAnyArg(1);
            state.PushNil();
        }
        return 1;
    }
}

}
