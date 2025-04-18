namespace YALuaToy.Core {

using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using YALuaToy.Compilation;
using YALuaToy.Compilation.Antlr;
using YALuaToy.Const;

/* ================== Standard Error ================== */

internal class LuaCoreError : Exception
{
    public LuaCoreError(string msg): base($"Core Bug: {msg}") { }
}

internal class RethrowingWrapError : Exception
{
    public readonly Exception error;

    public RethrowingWrapError(Exception error): base(error.Message) {
        this.error = error;
    }
}

public class LuaException : Exception
{
    internal readonly LuaValue errorValue;

    public virtual ThreadStatus threadStatus => ThreadStatus.ERRRUN;

    public LuaException(string errorMsg): this(new LuaString(errorMsg)) { }
    internal LuaException(LuaString errorMsg): base(errorMsg.Str) {
        errorValue = new LuaValue(errorMsg);
    }
    internal LuaException(in LuaValue errorValue): base(errorValue.ToString()) {
        this.errorValue = errorValue;
    }
}

/* ThreadStatus.YIELD */
internal class LuaYield : LuaException
{
    private static readonly LuaString msg = new LuaString("LuaYield");

    public LuaYield(): base(msg) { }

    public override ThreadStatus threadStatus => ThreadStatus.YIELD;
}

/* ThreadStatus.ERRRUN */
public class LuaRuntimeError : LuaException
{
    public LuaRuntimeError(string msg): base(msg) { }
    internal LuaRuntimeError(in LuaValue errorValue): base(errorValue) { }

    public override ThreadStatus threadStatus => ThreadStatus.ERRRUN;
}

/* ThreadStatus.ERRSYNTAX */
internal class LuaSyntaxError : LuaException
{
    public override ThreadStatus threadStatus => ThreadStatus.ERRSYNTAX;

    public LuaSyntaxError(IToken token): base(TokenErrorMsg(token)) { }
    public LuaSyntaxError(string hint, IToken token): base($"{TokenErrorMsg(token)}. Hint: {hint}") { }
    public LuaSyntaxError(string hint, int type):
        base($"Syntax error, lexer type: {LuaLexer.DefaultVocabulary.GetSymbolicName(type)}, {hint}") { }
    public LuaSyntaxError(string hint): base($"Syntax error, {hint}") { }

    public static string TokenErrorMsg(IToken token) {
        return $"Syntax error at line {token.Line}, column {token.Column}: {token.Text}";
    }
}

/* ThreadStatus.ERRMEM */
internal class LuaMemoryError : LuaException
{
    public LuaMemoryError(LuaState state): base(state.globalState.MemerrMsg) { }

    public override ThreadStatus threadStatus => ThreadStatus.ERRMEM;
}

/* ThreadStatus.ERRGCMM */
internal class LuaGCError : LuaException
{
    private static readonly LuaString msg = new LuaString("GC Error.");
    public LuaGCError(): base(msg) { }

    public override ThreadStatus threadStatus => ThreadStatus.ERRGCMM;
}

/* ThreadStatus.ERRERR */
internal class LuaErrorError : LuaException
{
    public LuaErrorError(string hint): base($"Error on handling error. Hint: {hint}") { }

    public override ThreadStatus threadStatus => ThreadStatus.ERRERR;
}

/* ================== Utils Error ================== */

internal class LuaInvalidLdx : LuaRuntimeError
{
    public LuaInvalidLdx(int invalidLdx): base($"Invalid stack index: {invalidLdx}") { }
    public LuaInvalidLdx(int invalidLdx, string hint): base($"Invalid stack index: {invalidLdx}. Hint: {hint}") { }
}

internal class LuaUnexpectedType : LuaRuntimeError
{
    public LuaUnexpectedType(LuaType actualType, string hint):
        base($"Unexpected type, current: {LuaConst.TypeName(actualType.Variant)}. Hint: {hint}") { }
    public LuaUnexpectedType(LuaType actualType, params LuaType[] expectedTypes):
        base($"Unexpected type, current: {LuaConst.TypeName(actualType.Variant)}, expected: {string.Join(", ", expectedTypes)}") { }
    public LuaUnexpectedType(string hint, LuaType actualType, params LuaType[] expectedTypes):
        base($"Unexpected type, current: {LuaConst.TypeName(actualType.Variant)}, expected: {string.Join(", ", expectedTypes)}. Hint: {hint}"
        ) { }
}

internal class LuaConcateError : LuaRuntimeError
{
    public LuaConcateError(in LuaValue lhs, in LuaValue rhs): base($"Can't concatenate two value, type1: {lhs.Type}, type2: {rhs.Type}") { }
}

internal class LuaToIntError : LuaRuntimeError
{
    public LuaToIntError(in LuaValue luaValue): base($"Can't represente by integer: {luaValue}") { }
    public LuaToIntError(in LuaValue lhs, in LuaValue rhs): this(GetErrorValue(lhs, rhs)) { }

    public static LuaValue GetErrorValue(in LuaValue lhs, in LuaValue rhs) {
        if (!lhs.ToInteger(out var _, ToIntMode.FLOOR))
            return lhs;
        return rhs;
    }
}

internal class LuaCompareError : LuaRuntimeError
{
    public LuaCompareError(in LuaValue l1, in LuaValue l2): base(GetMsg(l1, l2)) { }

    public static string GetMsg(in LuaValue l1, in LuaValue l2) {
        if (l1.Type.Raw == l2.Type.Raw)
            return $"Attempt to compare two {l1.Type} values.";
        else
            return $"Attempt to compare {l1.Type} with {l2.Type}.";
    }
}

internal class LuaIntOperationError : LuaUnexpectedType
{
    public LuaIntOperationError(in LuaValue luaValue, string hint): base(luaValue.Type, hint) { }
    public LuaIntOperationError(in LuaValue lhs, in LuaValue rhs, string hint): this(GetErrorValue(lhs, rhs), hint) { }

    public static LuaValue GetErrorValue(in LuaValue lhs, in LuaValue rhs) {
        if (!lhs.ToNumber(out var _))
            return lhs;
        return rhs;
    }
}

internal class LuaStackOverflow : LuaRuntimeError
{
    public LuaStackOverflow(): base("Lua thread stack overflow.") { }
    public LuaStackOverflow(string hint): base($"Lua thread stack overflow. Hint: {hint}") { }
}

internal class LuaNotSupportedError : LuaRuntimeError
{
    public LuaNotSupportedError(): base($"Feature not support.") { }
    public LuaNotSupportedError(string featureDesc): base($"Feature not support: {featureDesc}.") { }
}

/* ---------------- Parser ---------------- */

internal class LuaSemanticError : LuaSyntaxError
{
    public LuaSemanticError(string msg): base($"semantic Error: {msg}") { }
}

internal class LuaUnexpectedToken : LuaSyntaxError
{
    public LuaUnexpectedToken(IToken token): base($"unexpected token.", token) { }
}

internal class LuaOverLimitError : LuaSyntaxError
{
    public LuaOverLimitError(FuncState funcState, int limit, string what): base(GetMsg(funcState, limit, what)) { }

    public static string GetMsg(FuncState funcState, int limit, string what) {
        int line = funcState.Proto._firstLine;
        string
            where = line == 0 ? "main function" : $"function at line {line}";
        return $"Too many {what} (limit is {limit}) in {where}";
    }
}

internal class LuaUndefinedGoto : LuaSyntaxError
{
    public LuaUndefinedGoto(in LabelDesc pendingGoto): base(GetMsg(pendingGoto)) { }

    public static string GetMsg(in LabelDesc pendingGoto) {
        if (pendingGoto.name.Reserved)
            return $"{pendingGoto.name} at line {pendingGoto.line} not inside a loop"; /* 目前 reserved 的 goto 只有隐式生成的 break */
        else
            return $"no visible label '{pendingGoto.name}' for <goto> at line {pendingGoto.line}";
    }
}

}
