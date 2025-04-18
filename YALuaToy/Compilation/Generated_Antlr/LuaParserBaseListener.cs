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
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;

/// <summary>
/// This class provides an empty implementation of <see cref="ILuaParserListener"/>,
/// which can be extended to create a listener which only needs to handle a subset
/// of the available methods.
/// </summary>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.13.2")]
[System.Diagnostics.DebuggerNonUserCode]
[System.CLSCompliant(false)]
public partial class LuaParserBaseListener : ILuaParserListener {
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.start"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterStart([NotNull] LuaParser.StartContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.start"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitStart([NotNull] LuaParser.StartContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.chunk"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterChunk([NotNull] LuaParser.ChunkContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.chunk"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitChunk([NotNull] LuaParser.ChunkContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.block"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterBlock([NotNull] LuaParser.BlockContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.block"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitBlock([NotNull] LuaParser.BlockContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EmptyStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEmptyStat([NotNull] LuaParser.EmptyStatContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EmptyStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEmptyStat([NotNull] LuaParser.EmptyStatContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Assign</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAssign([NotNull] LuaParser.AssignContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Assign</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAssign([NotNull] LuaParser.AssignContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>FunctionCallStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFunctionCallStat([NotNull] LuaParser.FunctionCallStatContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>FunctionCallStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFunctionCallStat([NotNull] LuaParser.FunctionCallStatContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>LabelStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterLabelStat([NotNull] LuaParser.LabelStatContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>LabelStat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitLabelStat([NotNull] LuaParser.LabelStatContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Break</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterBreak([NotNull] LuaParser.BreakContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Break</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitBreak([NotNull] LuaParser.BreakContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Goto</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterGoto([NotNull] LuaParser.GotoContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Goto</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitGoto([NotNull] LuaParser.GotoContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Do</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterDo([NotNull] LuaParser.DoContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Do</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitDo([NotNull] LuaParser.DoContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>While</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterWhile([NotNull] LuaParser.WhileContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>While</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitWhile([NotNull] LuaParser.WhileContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Repeat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRepeat([NotNull] LuaParser.RepeatContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Repeat</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRepeat([NotNull] LuaParser.RepeatContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>If</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterIf([NotNull] LuaParser.IfContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>If</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitIf([NotNull] LuaParser.IfContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>NumericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterNumericFor([NotNull] LuaParser.NumericForContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>NumericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitNumericFor([NotNull] LuaParser.NumericForContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>GenericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterGenericFor([NotNull] LuaParser.GenericForContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>GenericFor</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitGenericFor([NotNull] LuaParser.GenericForContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>GlobalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterGlobalFunction([NotNull] LuaParser.GlobalFunctionContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>GlobalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitGlobalFunction([NotNull] LuaParser.GlobalFunctionContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>LocalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterLocalFunction([NotNull] LuaParser.LocalFunctionContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>LocalFunction</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitLocalFunction([NotNull] LuaParser.LocalFunctionContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>LocalAttr</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterLocalAttr([NotNull] LuaParser.LocalAttrContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>LocalAttr</c>
	/// labeled alternative in <see cref="LuaParser.stat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitLocalAttr([NotNull] LuaParser.LocalAttrContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.attnamelist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAttnamelist([NotNull] LuaParser.AttnamelistContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.attnamelist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAttnamelist([NotNull] LuaParser.AttnamelistContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.attrib"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAttrib([NotNull] LuaParser.AttribContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.attrib"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAttrib([NotNull] LuaParser.AttribContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.retstat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRetstat([NotNull] LuaParser.RetstatContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.retstat"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRetstat([NotNull] LuaParser.RetstatContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.label"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterLabel([NotNull] LuaParser.LabelContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.label"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitLabel([NotNull] LuaParser.LabelContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.funcname"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFuncname([NotNull] LuaParser.FuncnameContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.funcname"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFuncname([NotNull] LuaParser.FuncnameContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.varlist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterVarlist([NotNull] LuaParser.VarlistContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.varlist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitVarlist([NotNull] LuaParser.VarlistContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.namelist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterNamelist([NotNull] LuaParser.NamelistContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.namelist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitNamelist([NotNull] LuaParser.NamelistContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.explist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterExplist([NotNull] LuaParser.ExplistContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.explist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitExplist([NotNull] LuaParser.ExplistContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.exp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterExp([NotNull] LuaParser.ExpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.exp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitExp([NotNull] LuaParser.ExpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.var"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterVar([NotNull] LuaParser.VarContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.var"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitVar([NotNull] LuaParser.VarContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.var_name"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterVar_name([NotNull] LuaParser.Var_nameContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.var_name"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitVar_name([NotNull] LuaParser.Var_nameContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.prefixexp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterPrefixexp([NotNull] LuaParser.PrefixexpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.prefixexp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitPrefixexp([NotNull] LuaParser.PrefixexpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.prefixexp_without_functioncall"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterPrefixexp_without_functioncall([NotNull] LuaParser.Prefixexp_without_functioncallContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.prefixexp_without_functioncall"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitPrefixexp_without_functioncall([NotNull] LuaParser.Prefixexp_without_functioncallContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.prefixexp_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterPrefixexp_([NotNull] LuaParser.Prefixexp_Context context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.prefixexp_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitPrefixexp_([NotNull] LuaParser.Prefixexp_Context context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.functioncall_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFunctioncall_([NotNull] LuaParser.Functioncall_Context context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.functioncall_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFunctioncall_([NotNull] LuaParser.Functioncall_Context context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.functioncall"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFunctioncall([NotNull] LuaParser.FunctioncallContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.functioncall"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFunctioncall([NotNull] LuaParser.FunctioncallContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.args"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterArgs([NotNull] LuaParser.ArgsContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.args"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitArgs([NotNull] LuaParser.ArgsContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.functiondef"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFunctiondef([NotNull] LuaParser.FunctiondefContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.functiondef"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFunctiondef([NotNull] LuaParser.FunctiondefContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.funcbody"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFuncbody([NotNull] LuaParser.FuncbodyContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.funcbody"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFuncbody([NotNull] LuaParser.FuncbodyContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.parlist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterParlist([NotNull] LuaParser.ParlistContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.parlist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitParlist([NotNull] LuaParser.ParlistContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.tableconstructor"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterTableconstructor([NotNull] LuaParser.TableconstructorContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.tableconstructor"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitTableconstructor([NotNull] LuaParser.TableconstructorContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.fieldlist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFieldlist([NotNull] LuaParser.FieldlistContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.fieldlist"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFieldlist([NotNull] LuaParser.FieldlistContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.field"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterField([NotNull] LuaParser.FieldContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.field"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitField([NotNull] LuaParser.FieldContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.fieldsep"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFieldsep([NotNull] LuaParser.FieldsepContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.fieldsep"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFieldsep([NotNull] LuaParser.FieldsepContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.number"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterNumber([NotNull] LuaParser.NumberContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.number"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitNumber([NotNull] LuaParser.NumberContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LuaParser.string"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterString([NotNull] LuaParser.StringContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LuaParser.string"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitString([NotNull] LuaParser.StringContext context) { }

	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void EnterEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void ExitEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitTerminal([NotNull] ITerminalNode node) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitErrorNode([NotNull] IErrorNode node) { }
}
} // namespace YALuaToy.Compilation.Antlr
