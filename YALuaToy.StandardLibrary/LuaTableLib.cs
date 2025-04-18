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

internal static class LuaTableLib
{
    public const string LIB_NAME = "table";

    private static readonly List<(string, LuaCFunction)> tableFuncs = [
        ("unpack", _UnPack),
        ("insert", _Insert),
        /* my custom */
        ("print", _Print),
    ];

    internal static int OpenLib_Table(this LuaState state) {
        state.NewLib(tableFuncs);
        return 1;
    }

    /* ---------------- Lib Funcs ---------------- */

    private static int _UnPack(this LuaState state) {
        long start = state.GetIntegerArg(2, 1);
        long end   = state.IsNoneOrNil(3) ? state.LengthAt(1) : state.GetIntegerArg(3);
        if (start > end)
            return 0;                            /* empty range */
        ulong count = (ulong)end - (ulong)start; /* number of elements minus 1 (avoid overflows) */
        if (count >= uint.MaxValue || !state.LuaCheckStack((int)++count))
            throw new LuaRuntimeError($"too many results to unpack: {count}");
        for (long i = start; i < end; i++) /* push arg[start..end - 1] (to avoid overflows) */
            state.GetTable(1, i);
        state.GetTable(1, end); /* push last element */
        return (int)count;
    }
    private static int _Insert(this LuaState state) {
        long firstEmpty = state._GetLengthHelper(1, TableFlags.W) + 1;
        long pos; /* where to insert new element */
        switch (state.TopLdx) {
        case 2: {             /* called with only 2 arguments */
            pos = firstEmpty; /* insert new element at the end */
            break;
        }
        case 3: {
            pos = state.GetIntegerArg(2); /* 2nd argument is the position */
            if (!(1 <= pos && pos <= firstEmpty))
                throw new LuaArgError(state, 2, $"position out of bounds: {pos}, valid range: {1}~{firstEmpty}");
            for (long i = firstEmpty; i > pos; i--) { /* move up elements */
                state.GetTable(1, i - 1);
                state.SetTable(1, i); /* t[i] = t[i - 1] */
            }
            break;
        }
        default: {
            throw new LuaRuntimeError($"wrong number of arguments to 'insert', {state.TopLdx}");
        }
        }
        state.SetTable(1, pos); /* t[pos] = v */
        return 0;
    }

    private static int _Print(this LuaState state) {
        if (state.GetType(-1) == LuaConst.TTABLE)
            Console.WriteLine(state.GetValueString(-1));
        else
            Console.WriteLine(state.GetString(-1));
        return 0;
    }

    /* ---------------- Utils ---------------- */

    [Flags]
    private enum TableFlags {
        NONE = 0,
        R    = 1 << 0,
        W    = 1 << 1,
        LEN  = 1 << 2,
    }

    private static void _CheckTableArg(this LuaState state, int argLdx, TableFlags flags) {
        int  n = 1; /* number of elements to pop */
        bool GetField(string fieldName) {
            n++;
            return state.RawGetTable(argLdx + 1, fieldName) != LuaConst.TNIL;
        }
        if (state.GetType(argLdx) != LuaConst.TTABLE) {
            if (state.GetMetatable(argLdx) && /* must have metatable */     //
                ((flags & TableFlags.R) == 0 || GetField("__index")) &&     //
                ((flags & TableFlags.W) == 0 || GetField("__newindex")) &&  //
                ((flags & TableFlags.LEN) == 0 || GetField("__len"))) {     //
                state.Pop(n); /* pop metatable and tested metamethods */    //
            } else
                state.CheckArgType(argLdx, LuaConst.TTABLE); /* force an error */
        }
    }
    private static long _GetLengthHelper(this LuaState state, int argLdx, TableFlags flags) {
        state._CheckTableArg(argLdx, flags | TableFlags.LEN);
        return state.LengthAt(argLdx);
    }
}

}
