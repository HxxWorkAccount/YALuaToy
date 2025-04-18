//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.13.2
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from YALuaToy/Assets/LuaParser.g4 by ANTLR 4.13.2

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419

namespace YALuaToy.Compilation.Antlr {
using Antlr4.Runtime.Misc;
using IParseTreeListener = Antlr4.Runtime.Tree.IParseTreeListener;
using IToken = Antlr4.Runtime.IToken;

/// <summary>
/// This interface defines a complete listener for a parse tree produced by
/// <see cref="LuaParser"/>.
/// </summary>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.13.2")]
[System.CLSCompliant(false)]
public interface ILuaParserListener : IParseTreeListener {
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.start"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterStart([NotNull] LuaParser.StartContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.start"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitStart([NotNull] LuaParser.StartContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.chunk"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterChunk([NotNull] LuaParser.ChunkContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.chunk"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitChunk([NotNull] LuaParser.ChunkContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.block"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterBlock([NotNull] LuaParser.BlockContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.block"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitBlock([NotNull] LuaParser.BlockContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>EmptyStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterEmptyStat([NotNull] LuaParser.EmptyStatContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>EmptyStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitEmptyStat([NotNull] LuaParser.EmptyStatContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>Assign</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterAssign([NotNull] LuaParser.AssignContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>Assign</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitAssign([NotNull] LuaParser.AssignContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>FunctionCallStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFunctionCallStat([NotNull] LuaParser.FunctionCallStatContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>FunctionCallStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFunctionCallStat([NotNull] LuaParser.FunctionCallStatContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>LabelStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterLabelStat([NotNull] LuaParser.LabelStatContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>LabelStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitLabelStat([NotNull] LuaParser.LabelStatContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>Break</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterBreak([NotNull] LuaParser.BreakContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>Break</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitBreak([NotNull] LuaParser.BreakContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>Goto</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterGoto([NotNull] LuaParser.GotoContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>Goto</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitGoto([NotNull] LuaParser.GotoContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>Do</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterDo([NotNull] LuaParser.DoContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>Do</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitDo([NotNull] LuaParser.DoContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>While</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterWhile([NotNull] LuaParser.WhileContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>While</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitWhile([NotNull] LuaParser.WhileContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>Repeat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterRepeat([NotNull] LuaParser.RepeatContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>Repeat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitRepeat([NotNull] LuaParser.RepeatContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>If</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterIf([NotNull] LuaParser.IfContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>If</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitIf([NotNull] LuaParser.IfContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>NumericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterNumericFor([NotNull] LuaParser.NumericForContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>NumericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitNumericFor([NotNull] LuaParser.NumericForContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>GenericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterGenericFor([NotNull] LuaParser.GenericForContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>GenericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitGenericFor([NotNull] LuaParser.GenericForContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>GlobalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterGlobalFunction([NotNull] LuaParser.GlobalFunctionContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>GlobalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitGlobalFunction([NotNull] LuaParser.GlobalFunctionContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>LocalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterLocalFunction([NotNull] LuaParser.LocalFunctionContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>LocalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitLocalFunction([NotNull] LuaParser.LocalFunctionContext context);
	/// <summary>
	/// Enter a parse tree produced by the <c>LocalAttr</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterLocalAttr([NotNull] LuaParser.LocalAttrContext context);
	/// <summary>
	/// Exit a parse tree produced by the <c>LocalAttr</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitLocalAttr([NotNull] LuaParser.LocalAttrContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.attnamelist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterAttnamelist([NotNull] LuaParser.AttnamelistContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.attnamelist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitAttnamelist([NotNull] LuaParser.AttnamelistContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.attrib"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterAttrib([NotNull] LuaParser.AttribContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.attrib"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitAttrib([NotNull] LuaParser.AttribContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.retstat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterRetstat([NotNull] LuaParser.RetstatContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.retstat"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitRetstat([NotNull] LuaParser.RetstatContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.label"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterLabel([NotNull] LuaParser.LabelContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.label"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitLabel([NotNull] LuaParser.LabelContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.funcname"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFuncname([NotNull] LuaParser.FuncnameContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.funcname"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFuncname([NotNull] LuaParser.FuncnameContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.varlist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterVarlist([NotNull] LuaParser.VarlistContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.varlist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitVarlist([NotNull] LuaParser.VarlistContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.namelist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterNamelist([NotNull] LuaParser.NamelistContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.namelist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitNamelist([NotNull] LuaParser.NamelistContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.explist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterExplist([NotNull] LuaParser.ExplistContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.explist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitExplist([NotNull] LuaParser.ExplistContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.exp"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterExp([NotNull] LuaParser.ExpContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.exp"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitExp([NotNull] LuaParser.ExpContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.var"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterVar([NotNull] LuaParser.VarContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.var"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitVar([NotNull] LuaParser.VarContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.var_name"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterVar_name([NotNull] LuaParser.Var_nameContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.var_name"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitVar_name([NotNull] LuaParser.Var_nameContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.prefixexp"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterPrefixexp([NotNull] LuaParser.PrefixexpContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.prefixexp"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitPrefixexp([NotNull] LuaParser.PrefixexpContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.prefixexp_without_functioncall"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterPrefixexp_without_functioncall([NotNull] LuaParser.Prefixexp_without_functioncallContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.prefixexp_without_functioncall"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitPrefixexp_without_functioncall([NotNull] LuaParser.Prefixexp_without_functioncallContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.prefixexp_"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterPrefixexp_([NotNull] LuaParser.Prefixexp_Context context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.prefixexp_"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitPrefixexp_([NotNull] LuaParser.Prefixexp_Context context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.functioncall_"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFunctioncall_([NotNull] LuaParser.Functioncall_Context context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.functioncall_"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFunctioncall_([NotNull] LuaParser.Functioncall_Context context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.functioncall"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFunctioncall([NotNull] LuaParser.FunctioncallContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.functioncall"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFunctioncall([NotNull] LuaParser.FunctioncallContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.args"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterArgs([NotNull] LuaParser.ArgsContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.args"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitArgs([NotNull] LuaParser.ArgsContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.functiondef"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFunctiondef([NotNull] LuaParser.FunctiondefContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.functiondef"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFunctiondef([NotNull] LuaParser.FunctiondefContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.funcbody"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFuncbody([NotNull] LuaParser.FuncbodyContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.funcbody"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFuncbody([NotNull] LuaParser.FuncbodyContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.parlist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterParlist([NotNull] LuaParser.ParlistContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.parlist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitParlist([NotNull] LuaParser.ParlistContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.tableconstructor"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterTableconstructor([NotNull] LuaParser.TableconstructorContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.tableconstructor"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitTableconstructor([NotNull] LuaParser.TableconstructorContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.fieldlist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFieldlist([NotNull] LuaParser.FieldlistContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.fieldlist"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFieldlist([NotNull] LuaParser.FieldlistContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.field"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterField([NotNull] LuaParser.FieldContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.field"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitField([NotNull] LuaParser.FieldContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.fieldsep"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterFieldsep([NotNull] LuaParser.FieldsepContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.fieldsep"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitFieldsep([NotNull] LuaParser.FieldsepContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.number"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterNumber([NotNull] LuaParser.NumberContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.number"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitNumber([NotNull] LuaParser.NumberContext context);
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.string"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void EnterString([NotNull] LuaParser.StringContext context);
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.string"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	void ExitString([NotNull] LuaParser.StringContext context);
}
} // namespace YALuaToy.Compilation.Antlr
