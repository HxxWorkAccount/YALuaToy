namespace YALuaToy.Compilation {

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Misc;
using DFA = Antlr4.Runtime.Dfa.DFA;
using YALuaToy.Core;
using YALuaToy.Debug;
using YALuaToy.Const;
using YALuaToy.Compilation.Antlr;

using FrameIndex       = System.Byte;  /* 栈帧索引（0 表示栈帧起点，寄存器应该是从 1 开始的） */
using InstructionIndex = System.Int32; /* 指令索引 */

/* 文法翻译部分 */
internal partial class LuaCodeTranslator
{
    private const int MAX_VAR_COUNT = 200; /* 每个栈帧最大局部变量数，因实现限制，该值最大也不能超过 255 */

    /* 一些原本记在 LexState 上的数据就放在这了 */
    private LuaState    _state;
    private LuaTable    _constantsIMap;
    private DynamicData _dyd;
    private FuncState   _funcState;
    private LuaString   _srouce;

    private Dictionary<string, LuaString> _reservedWords;

    public LuaCodeTranslator(LuaState state, LuaTable constantsIMap, LuaString source) {
        _state         = state;
        _constantsIMap = constantsIMap;
        _srouce        = source;
        _dyd           = new DynamicData();
        _reservedWords = new Dictionary<string, LuaString>();

        LuaString[] reservedWords = LuaGlobalState.ReservedWords();
        foreach (var word in reservedWords) {
            _reservedWords.Add(word.Str, word);
        }
    }

    /* ---------------- Translation Handler ---------------- */

    public LClosure TranslateStart(LuaParser.StartContext context) {
        _OpenFunc("main");
        FuncState funcState      = _funcState;
        _funcState.Proto._vararg = true;

        ExpDesc env = new ExpDesc(ExpType.LOCAL, (FrameIndex)0); /* 初始化环境变量，位置在 0 号栈帧（初始栈帧的栈基不是函数，而是环境变量） */
        _NewUpvalue(_funcState, LuaGlobalState.env, env);        /* 将环境变量设为上值 */

        _TranslateChunk(context.chunk());

        LClosure lclosure = new LClosure(_funcState.Proto, 1); /* 新建一个 LClosure 作为 main 函数，只有一个上值（env） */
        _CloseFunc(context.Stop == null ? 0 : context.Stop.Line);      /* 空文件的 Stop 是 null */

        LuaDebug.Assert(funcState.Prev == null);
        LuaDebug.Assert(funcState.Proto.UpvalueCount == 1);

        /* all scopes should be correctly finished */
        LuaDebug.Assert(_dyd.activeVarList.Count == 0);
        LuaDebug.Assert(_dyd.pendingGotoList.Count == 0);
        LuaDebug.Assert(_dyd.labelList.Count == 0);

        _state.PushStack(lclosure);
        return lclosure; /* 返回，但其实栈上也压入了 */
    }
    private void _TranslateChunk(LuaParser.ChunkContext context) {
        /* 在这里完成编译 main 函数（即文件函数） */
        _TranslateStats(context.block(), false);
    }
    private void _TranslateBlock(LuaParser.BlockContext context, bool loop, bool inRepeat = false) {
        _EnterBlock(loop);
        _TranslateStats(context, inRepeat);
        _LeaveBlock(context.Stop.Line);
    }
    private void _TranslateStats(
        LuaParser.BlockContext context,
        bool                   inRepeat       = false,
        int                    startStatIndex = 0,
        int                    statCount      = 9999999,
        bool                   readRetStat    = true
    ) {
        LuaParser.StatContext[] stats = context.stat();
        int i                         = startStatIndex;
        int end                       = Math.Clamp(i + statCount, 0, stats.Length);

        /* 这里先把 lastIdx 算出来 */
        int lastIdx = 9999999;
        if (context.retstat() == null) {
            for (lastIdx = stats.Length - 1; lastIdx >= i; lastIdx--) {
                LuaParser.StatContext stat = stats[lastIdx];
                if (stat is LuaParser.EmptyStatContext || stat is LuaParser.LabelStatContext)
                    continue;
                break;
            }
        }

        for (; i < end; i++) {
            LuaParser.StatContext stat = stats[i];
            _TranslateStat(stat, i >= lastIdx, inRepeat);
        }
        if ((i == stats.Length || stats.Length == 0) && readRetStat && context.retstat() != null)
            _TranslateRetstat(context.retstat());
    }

    /* 语句（注，有部分语句只是转发文法，这个在 LuaParser 的 partial 类里调用了） */
    private void _TranslateStat(LuaParser.StatContext context, bool last, bool inRepeat) {
        _EnterLevel();
        context._Translate(this, last, inRepeat);
        LuaDebug.Assert(_funcState.Proto.FrameSize >= _funcState.FreeReg && _funcState.FreeReg >= _funcState.activeLocalVarCount);
        _funcState.FreeReg = _funcState.activeLocalVarCount;
        _LeaveLevel();
    }
    internal void _TranslateEmptyStat(LuaParser.EmptyStatContext context) {
        /* 空语句不生成任何代码 */
    }
    internal void _TranslateAssign(LuaParser.AssignContext context) {
        ExpDesc             lastExp  = new ExpDesc();
        LinkedList<ExpDesc> varlist  = _TranslateVarlist(context.varlist());
        int                 expCount = _TranslateExplist(context.explist(), lastExp); /* 这一步已经分配 n-1 个寄存器，并把 rhs 按序压入栈 */
        if (varlist.Count != expCount) {
            _AdjustAssignment(varlist.Count, expCount, lastExp, context.Start.Line);
        } else {
            _funcState.SimplifyMultiResult(lastExp);
            _funcState.AssignToVar(varlist.Last.Value, lastExp, context.Start.Line); /* 这一步会释放最后一个寄存器 */
            varlist.RemoveLast();
        }

        /* 执行实际赋值操作，并释放 rhs 占用的寄存器 */
        foreach (var varExp in varlist.Reverse()) { /* 此时栈上新增的表达式数量已经和 varlist 长度完全一样 */
            ExpDesc mockExp = new ExpDesc(ExpType.NONRELOC, _funcState.FreeReg - 1);
            _funcState.AssignToVar(varExp, mockExp, context.Start.Line); /* 这一步只会释放最后一个寄存器，所以要倒序 */
        }
    }
    internal void _TranslateFunctionCallStat(LuaParser.FunctionCallStatContext context) {
        ExpDesc func = new ExpDesc();
        _TranslateFunctioncall(context.functioncall(), func);
        _funcState.SetResultCount(func, 0); /* 纯语句的 function call 不需要返回值 */
    }
    internal void _TranslateBreak(LuaParser.BreakContext context) {
        _TranslateGotoHelper(_funcState.GeneratePendingJump(context.Start.Line), context.Start.Line, _NewLuaString("break"));
    }
    internal void _TranslateGoto(LuaParser.GotoContext context) {
        _TranslateGotoHelper(_funcState.GeneratePendingJump(context.Start.Line), context.Start.Line, _NewLuaString(context.NAME().GetText()));
    }
    internal void _TranslateDo(LuaParser.DoContext context) {
        _TranslateBlock(context.block(), false);
    }
    internal void _TranslateWhile(LuaParser.WhileContext context) {
        InstructionIndex loopBegin  = _funcState.MarkLastTarget();           /* while 循环首地址 */
        InstructionIndex whileFalse = _TranslateConditionExp(context.exp()); /* 表达式为 false 时跳过函数体 */
        _EnterBlock(true);                                                   /* 这里调用 EnterBlock 是为了给 break 用的 */
        _TranslateBlock(context.block(), false);
        _funcState.GenerateJump(loopBegin, context.block().Stop.Line); /* 循环体结束后，生成一个跳转指令跳回 while 循环首地址 */
        _LeaveBlock(context.Stop.Line);
        _funcState.PatchTargetToNext(whileFalse); /* 回填 falseList 的结束位置 */
    }
    internal void _TranslateRepeat(LuaParser.RepeatContext context) {
        int          repeatBegin = _funcState.MarkLastTarget();
        BlockControl exBlock     = _EnterBlock(true);  /* loop block */
        BlockControl inBlock     = _EnterBlock(false); /* scope block，until exp 属于这个块内 */
        _TranslateStats(context.block(), true);        /* 要标记是在 repeat 里面 */
        InstructionIndex untilFalse = _TranslateConditionExp(context.exp());
        if (inBlock.hasUpvalue) /* 处理 until exp 内的上值 */
            _funcState.PatchClose(untilFalse, inBlock.activeOutVarCount);
        _LeaveBlock(context.block().Stop.Line);
        _funcState.PatchTarget(untilFalse, repeatBegin); /* until false 时，跳回起点 */
        _LeaveBlock(context.Stop.Line);
    }
    internal void _TranslateIf(LuaParser.IfContext context) {
        InstructionIndex escapeList = FuncState.NO_JUMP; /* 以 if 结构结束点为目标的跳转指令列表 */
        var              exps       = context.exp();
        var              blocks     = context.block();
        for (int i = 0; i < exps.Length; i++) {
            bool last = i == exps.Length - 1 && context.ELSE() == null;
            _TranslateTestBlock(exps[i], blocks[i], last, ref escapeList);
        }
        if (context.ELSE() != null)
            _TranslateBlock(blocks[blocks.Length - 1], false);
        _funcState.PatchTargetToNext(escapeList);
    }
    internal void _TranslateNumericFor(LuaParser.NumericForContext context) {
        _EnterBlock(true);
        /* 解析表达式，求值放到寄存器上（保证寄存器是最后一个有效的），并返回寄存器编号 */
        FrameIndex TranslateForExp(LuaParser.ExpContext expContext) {
            ExpDesc e = new ExpDesc();
            _TranslateExp(expContext, e);
            _funcState.CloseToNextReg(e, expContext.Start.Line);
            LuaDebug.Assert(e.Type == ExpType.NONRELOC);
            return e.Reg;
        }
        FrameIndex loopControlVar = _funcState.FreeReg;
        _NewLocalVar("(for index)"); /* 内部循环控制变量 */
        _NewLocalVar("(for limit)");
        _NewLocalVar("(for step)");
        _NewLocalVar(context.NAME().GetText()); /* 外部循环控制变量 */
        TranslateForExp(context.exp(0));
        TranslateForExp(context.exp(1));
        if (context.exp(2) != null) {
            TranslateForExp(context.exp(2));
        } else { /* 如果没有步长，则手动设置 (for step) 默认为 1 */
            _funcState.GenerateLoadK(_funcState.FreeReg, (uint)_funcState.AddConstant(1), context.Start.Line);
            _funcState.ReserveRegs(1);
        }
        _TranslateForBody(context.block(), loopControlVar, 1, true, context.Start.Line);
        _LeaveBlock(context.Stop.Line);
    }
    internal void _TranslateGenericFor(LuaParser.GenericForContext context) {
        _EnterBlock(true);
        ExpDesc    lastRhs        = new ExpDesc();
        FrameIndex loopControlVar = _funcState.FreeReg;
        _NewLocalVar("(for generator)"); /* 内部循环控制变量 */
        _NewLocalVar("(for state)");
        _NewLocalVar("(for control)");
        FrameIndex userVarCount = _TranslateNamelist(context.namelist()); /* 用户变量 */
        int        rhsCount     = _TranslateExplist(context.explist(), lastRhs);
        _AdjustAssignment(3, rhsCount, lastRhs, context.explist().Start.Line);
        _funcState.CheckStack(3);
        _TranslateForBody(context.block(), loopControlVar, userVarCount, false, context.Start.Line);
        _LeaveBlock(context.Stop.Line);
    }
    internal void _TranslateGlobalFunction(LuaParser.GlobalFunctionContext context) {
        ExpDesc var      = new ExpDesc(); /* 函数名找到的变量 */
        ExpDesc closure  = new ExpDesc(); /* 存放闭包的变量 */
        bool    isMethod = _TranslateFuncname(context.funcname(), var);
        _TranslateFuncbody(context.funcbody(), closure, isMethod, context.funcname().GetText(), context.Start.Line);
        _funcState.AssignToVar(var, closure, context.Start.Line);
        _funcState.ChangeCurrInstLine(context.Start.Line);
    }
    internal void _TranslateLocalFunction(LuaParser.LocalFunctionContext context) {
        ExpDesc closure = new ExpDesc();
        _NewLocalVar(context.NAME().GetText());
        _UpdateLocalVars(1);
        _TranslateFuncbody(context.funcbody(), closure, false, context.NAME().Symbol.Text, context.Start.Line); /* 这里会执行寄存器分配 */
        _GetLocalVar(_funcState, closure.Reg).startPC = _funcState.Pc;
    }
    internal void _TranslateLocalAttr(LuaParser.LocalAttrContext context) {
        /* 局部变量声明。该函数相比 `assign` 简单很多。主要有以下原因：
           1. 不用考虑索引变量
           2. 不用考虑冲突情况
           3. 不用分配寄存器，explist 会按序压入栈，用 adjust_assign 处理以下即可 */
        FrameIndex lhsCount = _TranslateAttnamelist(context.attnamelist());
        int        rhsCount = 0;
        ExpDesc    lastRhs  = new ExpDesc();
        if (context.ASSIGN() != null)
            rhsCount = _TranslateExplist(context.explist(), lastRhs);
        _AdjustAssignment(lhsCount, rhsCount, lastRhs, context.Start.Line);
        _UpdateLocalVars(lhsCount);
    }

    /* 解析 for 循环体。该函数有点复杂，且能同时处理数值 for 和泛型 for；
       对于数值 for，栈布局为：loop_var, limit, step, user_loop_var；
       对于泛型 for，栈布局为：generator, constant_state, first_var, user_var1, user_var2, ...
       进入该函数时，三个控制变量的 局部变量信息、寄存器都已分配，但还需要更新 dyd */
    private void _TranslateForBody(
        LuaParser.BlockContext blockContext,
        FrameIndex             loopControlVar, /* 循环控制变量 */
        FrameIndex             varCount,       /* 循环变量数量 */
        bool                   numeric,        /* 是否为数值 for，否则为泛型 for */
        int                    forStartline
    ) {
        _UpdateLocalVars(3); /* 更新 3 个控制变量的 startPC，保存控制变量的初始值 */
        int prepareInstIndex;
        if (numeric) /* 数值 for 的循环需要生成一段准备代码，这是准备代码的起始位置 */
            prepareInstIndex = _funcState.GenerateCodeAsBx(OpCode.FORPREP, loopControlVar, FuncState.NO_JUMP, forStartline);
        else /* 如果是泛型 for，则直接跳到控制语句 */
            prepareInstIndex = _funcState.GeneratePendingJump(forStartline);

        /* 函数体 */
        _EnterBlock(false);               /* 循环块在外面 for 语句就已经进入了，现在是 scope block */
        _UpdateLocalVars(varCount);       /* 更新用户循环变量 */
        _funcState.ReserveRegs(varCount); /* 为用户循环变量预留寄存器 */
        _TranslateBlock(blockContext, false);
        _LeaveBlock(blockContext.Stop.Line);

        /* 控制语句 */
        _funcState.PatchTargetToNext(prepareInstIndex); /* 进 for 语句时先跳过函数体，直接进入控制语句 */
        InstructionIndex forend;                        /* 整个 for 语句结束位置 */
        if (numeric)
            forend = _funcState.GenerateCodeAsBx(OpCode.FORLOOP, loopControlVar, FuncState.NO_JUMP, forStartline);
        else {
            _funcState.GenerateCodeABC(OpCode.TFORCALL, loopControlVar, 0, (ushort)varCount, forStartline);
            _funcState.ChangeCurrInstLine(forStartline);
            forend = _funcState.GenerateCodeAsBx(OpCode.TFORLOOP, (FrameIndex)(loopControlVar + 2), FuncState.NO_JUMP, forStartline);
        }
        _funcState.PatchTarget(forend, prepareInstIndex + 1); /* 循环结束为止直接跳回函数体开头，如果条件判断为 false，则会跳过这个跳转 */
        _funcState.ChangeCurrInstLine(forStartline);
    }
    private void _TranslateTestBlock(  //
        LuaParser.ExpContext   condContext,
        LuaParser.BlockContext blockContext,
        bool                   last,
        ref InstructionIndex   escapeList
    ) {
        ExpDesc          cond = new ExpDesc();
        InstructionIndex falseList; /* 条件表达式为 false 时到跳转列表 */
        _TranslateExp(condContext, cond);

        int        i         = 0;
        var        stats     = blockContext.stat();
        IParseTree firstStat = null;
        if (stats.Length > 0)
            firstStat = blockContext.GetChild(0);

        /* 如果 then 后立刻是 goto 或 break，则要特殊处理一下 */
        if (firstStat is LuaParser.GotoContext || firstStat is LuaParser.BreakContext) {
            /* 把 if 条件表达式与后边的 goto 绑定，组成一条跳转：当 true 时执行跳转，否则继续执行 */
            _funcState.CreateTrueJump(cond, condContext.Start.Line);
            _EnterBlock(false);
            // _TranslateStats(blockContext, false, i++, 1, false);
            string labelName = "break";
            if (firstStat is LuaParser.GotoContext gotoStat)
                labelName = gotoStat.NAME().Symbol.Text;
            _TranslateGotoHelper(cond.trueList, blockContext.Start.Line, _NewLuaString(labelName)); /* 处理第一行的 goto 或 break */
            i++;

            /* 为了防止真的执行到后边的内容，这里要再插入一条跳转指令，跳转到 if 结尾（即，实际上 if 条件为 false 时的情况） */
            for (; i < stats.Length; i++) { /* 在连续的空语句后插入结束跳转 */
                if (stats[i] is LuaParser.EmptyStatContext emptyStat)
                    _TranslateEmptyStat(emptyStat);
                else
                    break;
            }
            if (i == blockContext.ChildCount) { /* 如果后边全是空语句，直接退出得了，不需要其他步骤 */
                _LeaveBlock(blockContext.Stop.Line);
                return;
            } else { /* 如果还有有效语句，则在有效语句前插入跳出 */
                falseList = _funcState.GeneratePendingJump(blockContext.Start.Line);
            }
        } else {
            _funcState.CreateFalseJump(cond, condContext.Start.Line); /* 正常的 if 语句，设为：当 false 时跳转到 if 结束点 */
            _EnterBlock(false);
            falseList = cond.falseList;
        }
        /* 解析剩余的 block 代码 */
        _TranslateStats(blockContext, startStatIndex: i);
        _LeaveBlock(blockContext.Stop.Line);
        if (!last) /* 如果不是最后一个 if/elseif/else 块，则在解析下一个块前先插入一条跳转，跳到整个块结束点 */
            _funcState.JoinJumpList(ref escapeList, _funcState.GeneratePendingJump(blockContext.Stop.Line));
        _funcState.PatchTargetToNext(falseList); /* 判断失败，则执行块后边的代码（可能是下一个 elseif/else，也可能就直接到结束点了） */
    }

    /* 表达式 */
    private void _TranslateExp(LuaParser.ExpContext context, ExpDesc output) {
        _EnterLevel();
        if (context.NIL() != null) {
            output.Reset(ExpType.NIL);
        } else if (context.FALSE() != null) {
            output.Reset(ExpType.FALSE);
        } else if (context.TRUE() != null) {
            output.Reset(ExpType.TRUE);
        } else if (context.number() != null) {
            _TranslateNumber(context.number(), output);
        } else if (context.@string() != null) {
            _TranslateString(context.@string(), output);
        } else if (context.DOTS() != null) {
            LuaParserUtils.Check(_funcState.Proto.Vararg, "cannot use '...' outside a vararg function");
            output.Reset(ExpType.VARARG, _funcState.GenerateCodeABC(OpCode.VARARG, 0, 1, 0, context.Start.Line));
        } else if (context.functiondef() != null) {
            _TranslateFunctiondef(context.functiondef(), false, output);
        } else if (context.prefixexp() != null) {
            _TranslatePrefixexp(context.prefixexp(), output);
        } else if (context.tableconstructor() != null) {
            _TranslateTableconstructor(context.tableconstructor(), output);
        } else if (context.GetChild(0) is ITerminalNode terminal) { /* unary operation */
            LToken  ltoken = (LToken)terminal.Symbol;
            UnaryOp op     = ltoken.UnaryOp;
            LuaDebug.Assert(op != UnaryOp.NOUNOPR);
            _TranslateExp(context.exp(0), output);
            _funcState.ApplyUnaryOp(op, output, context.Start.Line);
        } else { /* binary operation */
            ITerminalNode binaryTerminal = context.GetChild(1) as ITerminalNode;
            LToken        ltoken         = (LToken)binaryTerminal.Symbol;
            BinaryOp      op             = ltoken.BinaryOp;
            LuaDebug.Assert(op != BinaryOp.NOBINOPR);
            _TranslateExp(context.left, output);
            _funcState.PrepareBinaryOp(op, output, ltoken.Line);
            ExpDesc right = new ExpDesc();
            _TranslateExp(context.right, right);
            _funcState.ApplyBinaryOp(op, output, right, context.Start.Line);
        }
        _LeaveLevel();
    }

    /* 杂项结构 */
    private FrameIndex _TranslateAttnamelist(LuaParser.AttnamelistContext context) {
        ITerminalNode[] names = context.NAME();
        for (int i = 0; i < names.Length; i++) {
            _NewLocalVar(names[i].GetText());
            _TranslateAttrib(context.attrib(i));
        }
        return (FrameIndex)names.Length;
    }
    private void _TranslateAttrib(LuaParser.AttribContext context) {
        /* 暂不支持 attrib */
    }
    private void _TranslateRetstat(LuaParser.RetstatContext context) {
        ExpDesc    lastExp     = new ExpDesc();
        FrameIndex firstResult = 0;
        int        resultCount = 0;
        if (context.explist() != null) {
            resultCount = _TranslateExplist(context.explist(), lastExp); /* 表达式的值压入栈 */
            if (lastExp.Type.HasMultiResults()) {
                _funcState.SetMultiResults(lastExp);                    /* 设置表达式为多返回值 */
                if (lastExp.Type == ExpType.CALL && resultCount == 1) { /* 尾调用 */
                    /* 这里可以直接把 opcde 从 OP_CALL 改为 OP_TAILCALL，两者参数是一样的 */
                    Instruction inst    = _funcState.GetInstruction(lastExp);
                    Instruction newInst = inst.CoplaceOp(OpCode.TAILCALL);
                    _funcState.SetInstruction(lastExp, newInst);
                    LuaDebug.Assert(newInst.A.RKValue == _funcState.activeLocalVarCount);
                }
                firstResult = _funcState.activeLocalVarCount; /* _TranslateExplist 不会分配局部变量，所以可以用 nactvar 获取寄存器起点 */
                resultCount = LuaConst.MULTRET;
            } else {
                if (resultCount == 1)
                    firstResult = _funcState.Close(lastExp, context.Stop.Line);
                else {                                                     /* 多返回值，但数量固定 */
                    _funcState.CloseToNextReg(lastExp, context.Stop.Line); /* 把最后一个值压入栈顶，这一步 TranslateExplist 没有做 */
                    firstResult = _funcState.activeLocalVarCount;
                    LuaDebug.Assert(resultCount == _funcState.FreeReg - firstResult);
                }
            }
        }
        _funcState.GenerateReturn(firstResult, resultCount, context.Start.Line);
    }
    internal void _TranslateLabel(LuaParser.LabelContext context, bool lastStat, bool inRepeat) {
        /* 检查是否有重复标签 */
        void CheckRepeatedLabel(List<LabelDesc> labels, LuaString labelName_) {
            for (int i = _funcState.block.firstLabelIdx; i < labels.Count; i++) {
                if (!labelName_.Equals(labels[i].name))
                    continue;
                throw new LuaSemanticError($"label '{labelName_.Str}' already defined on line {labels[i]}");
            }
        }
        int             line      = context.Start.Line;
        LuaString       labelName = _NewLuaString(context.NAME().GetText());
        List<LabelDesc> labelList = _dyd.labelList;
        CheckRepeatedLabel(labelList, labelName);
        int newLabelIdx = _NewLabelOrGoto(labelList, labelName, line, _funcState.MarkLastTarget());
        if (lastStat && !inRepeat) { /* 如果 label 是块的最后一个语句，则活跃变量数部统计当前块内的。注意 repeat 语句不算，因为后面还有 until */
            var copy                 = labelList[newLabelIdx];
            copy.activeLocalVarCount = _funcState.block.activeOutVarCount;
            labelList[newLabelIdx]   = copy;
        }
        _TryFindGotos(labelList[newLabelIdx]);
    }
    private bool _TranslateFuncname(LuaParser.FuncnameContext context, ExpDesc output) {
        /* output 为变量表达式，返回是否为方法
           e.g. `function mytable.foo() ... end`, `function mytable:foo() ... end` */
        bool isMethod = false;
        var  names    = context.NAME();

        _TranslatePrimaryVar(output, names[0].GetText(), names[0].Symbol.Line); /* 处理第一个 NAME */

        var dots = context.DOT();
        for (int i = 0; i < dots.Length; i++) { /* 处理通过 '.' 索引的字段 */
            ITerminalNode name = names[1 + i];
            _TranslateFieldSelector(output, name.GetText(), name.Symbol.Line);
        }

        if (context.COL() != null) { /* 处理通过 ':' 索引的字段 */
            isMethod = true;
            _TranslateFieldSelector(output, names[names.Length - 1].GetText(), names[names.Length - 1].Symbol.Line);
        }
        return isMethod;
    }
    private LinkedList<ExpDesc> _TranslateVarlist(LuaParser.VarlistContext context) { /* 仅用于 assignment */
        LinkedList<ExpDesc> list = new LinkedList<ExpDesc>();
        foreach (LuaParser.VarContext var in context.var ()) {
            ExpDesc varExp = new ExpDesc();
            _TranslateVar(var, varExp);
            if (varExp.Type != ExpType.INDEXED) /* 处理赋值冲突 */
                _PreventAssignConflict(list, varExp, var.Start.Line);
            list.AddLast(varExp);
            _CheckLimit(list.Count + _state.cCalls, LuaConfig.LUAI_MAXCCALLS, "C levels");
        }
        return list;
    }
    private FrameIndex _TranslateNamelist(LuaParser.NamelistContext context) {
        /* 返回 name 数量 */
        foreach (var nameToken in context.NAME())
            _NewLocalVar(nameToken.GetText());
        return (FrameIndex)context.NAME().Length;
    }
    private int _TranslateExplist(LuaParser.ExplistContext context, ExpDesc lastExp) {
        /* 会**把表达式压入栈（除了后一个）**，返回表达式数量，最后一个表达式通过 lastExp 返回 */
        var exps = context.exp();
        for (int i = 0; i < exps.Length; i++) {
            _TranslateExp(exps[i], lastExp);
            if (i != exps.Length - 1) /* 除了最后一个其他都求值并压入栈中，最后一个要做 vararg 处理 */
                _funcState.CloseToNextReg(lastExp, context.Start.Line);
        }
        return exps.Length;
    }

    /* 变量 */
    private void _TranslateVar(LuaParser.VarContext context, ExpDesc output) {
        if (context.var_name() != null) {
            _TranslateVar_name(context.var_name(), output);
        } else {
            _TranslatePrefixexp(context.prefixexp(), output); /* primary */
            _TranslateIndex(output, context.exp(), context.NAME());
        }
    }
    private void _TranslateVar_name(LuaParser.Var_nameContext context, ExpDesc output) {
        _TranslatePrimaryVar(output, context.NAME().GetText(), context.NAME().Symbol.Line);
    }
    private void _TranslateIndex(ExpDesc prefix, LuaParser.ExpContext exp, ITerminalNode name) {
        if (name != null) { /* e.g. a.b */
            _TranslateFieldSelector(prefix, name.GetText(), name.Symbol.Line);
        } else { /* e.g. a[b].c */
            ExpDesc key = new ExpDesc();
            _funcState.Close_ExceptUpval(prefix, exp.Start.Line);
            _TranslateIndexedKey(exp, key);
            _funcState.Index(prefix, key, exp.Start.Line);
        }
    }

    /* 特殊表达式：被索引/调用前缀、函数调用 */
    private void _TranslatePrefixexp(LuaParser.PrefixexpContext context, ExpDesc output) {
        if (context.prefixexp_without_functioncall() != null) {
            _TranslatePrefixexp_without_functioncallContext(context.prefixexp_without_functioncall(), output);
        } else { /* e.g. func().x */
            _TranslateFunctioncall(context.functioncall(), output);
            _TranslatePrefixexp_(context.prefixexp_(), output);
        }
    }
    private void _TranslatePrefixexp_(LuaParser.Prefixexp_Context context, ExpDesc prefix) { /* 这里是后缀部分的处理 */
        if (_IsEmptyContext(context))
            return;
        _TranslateIndex(prefix, context.exp(), context.NAME()); /* 处理索引 */
        _TranslatePrefixexp_(context.prefixexp_(), prefix);
    }
    private void _TranslatePrefixexp_without_functioncallContext(LuaParser.Prefixexp_without_functioncallContext context, ExpDesc output) {
        if (context.var_name() != null) { /* e.g. var.a | var[a] */
            _TranslateVar_name(context.var_name(), output);
            _TranslatePrefixexp_(context.prefixexp_(), output);
        } else { /* e.g. (exp).a | (exp)[a] */
            _TranslateExp(context.exp(), output);
            _funcState.Simplify_ExceptJump(output, context.exp().Start.Line);
            _TranslatePrefixexp_(context.prefixexp_(), output);
        }
    }
    internal void _TranslateFunctioncall(LuaParser.FunctioncallContext context, ExpDesc output) {
        _TranslatePrefixexp_without_functioncallContext(context.prefixexp_without_functioncall(), output); /* prefix */
        _TranslateCall(context.args(), context.NAME(), context.functioncall_(), output, context.Start.Line);
    }
    private void _TranslateFunctioncall_(LuaParser.Functioncall_Context context, ExpDesc prefix) {
        if (_IsEmptyContext(context))
            return;
        _TranslateCall(context.args(), context.NAME(), context.functioncall_(), prefix, context.Start.Line);
    }
    private void _TranslateCall(  //
        LuaParser.ArgsContext          argsContext,
        ITerminalNode                  name,
        LuaParser.Functioncall_Context nextcall,
        ExpDesc                        prefix,
        int                            line
    ) {
        if (name != null) { /* e.g. print "a:b(c)" */
            ExpDesc key = new ExpDesc();
            _AddName(name.GetText(), key);
            _funcState.CreateSelf(prefix, key, name.Symbol.Line);
            _TranslateArgs(argsContext, prefix, line);
            _TranslateFunctioncall_(nextcall, prefix);
        } else { /* e.g. print("hello world") */
            _funcState.CloseToNextReg(prefix, argsContext.Start.Line);
            _TranslateArgs(argsContext, prefix, line);
            _TranslateFunctioncall_(nextcall, prefix);
        }
    }
    private void _TranslateArgs(LuaParser.ArgsContext context, ExpDesc func, int funcStartLine) {
        /* 解析实参列表，并完成【函数调用】的字节码生成。
           func 输入时是闭包表达式，类型为 NONRELOC，寄存器位于栈顶；
           func 输出时为调用表达式，类型为 CALL */

        ExpDesc lastArg = new ExpDesc();
        if (context.@string() != null) { /* e.g. print "hello world" */
            _TranslateString(context.@string(), lastArg);
        } else if (context.tableconstructor() != null) { /* e.g. func { 1, 2, 3 } */
            _TranslateTableconstructor(context.tableconstructor(), lastArg);
        } else if (context.explist() != null) { /* 非空列表 */
            _TranslateExplist(context.explist(), lastArg);
            _funcState.SetMultiResults(lastArg); /* 实参列表最后一个参数可以有多返回值 */
        }

        LuaDebug.AssertExpType(func, ExpType.NONRELOC); /* 必须已求值到栈顶 */
        FrameIndex base_ = func.Reg;                    /* 闭包所在寄存器 */
        int        argCount;
        if (lastArg.Type.HasMultiResults())
            argCount = LuaConst.MULTRET;
        else {
            if (lastArg.Type != ExpType.VOID)
                _funcState.CloseToNextReg(lastArg, context.Stop.Line); /* 关闭最后一个参数 */
            argCount = _funcState.FreeReg - (base_ + 1);               /* 然后求出实际数量 */
        }
        /* 注意最后的 2，这意味着 CALL 类型的 ExpDesc 在初始时默认只返回 1 个返回值，这点其他地方会用到 */
        func.Reset(ExpType.CALL, _funcState.GenerateCodeABC(OpCode.CALL, base_, (ushort)(argCount + 1), 2, context.Start.Line));
        _funcState.ChangeCurrInstLine(funcStartLine);
        _funcState.FreeReg = (FrameIndex)(base_ + 1); /* 这里预留了一个结果，因为上面说了返回值默认为 1。不过这个后面可以改 */
    }

    /* 函数声明 */
    private void _TranslateFunctiondef(LuaParser.FunctiondefContext context, bool isMethod, ExpDesc output) {
        _TranslateFuncbody(context.funcbody(), output, isMethod, "closure", context.Start.Line);
    }
    private void _TranslateFuncbody(LuaParser.FuncbodyContext context, ExpDesc output, bool isMethod, string funcname, int startLine) {
        /* 会占用一格寄存器，分配闭包 */
        _OpenFunc(funcname, startLine, context.Stop.Line);
        if (isMethod) {
            _NewLocalVar("self"); /* 如果是方法，则先创建参数 self */
            _UpdateLocalVars(1);
        }
        _TranslateParlist(context.parlist());
        _TranslateStats(context.block(), false);
        _GenerateClosureCode(output, context.Start.Line); /* 生成“闭包生成代码”，绑定的 proto 是直接用最新的 prot */
        _CloseFunc(context.Stop.Line);
    }
    private void _TranslateParlist(LuaParser.ParlistContext context) {
        FrameIndex paramCount    = 0;
        _funcState.Proto._vararg = false;

        /* 三种 parlist */
        if (_IsEmptyContext(context)) {
            /* 空参数列表，什么都不做 */
        } else if (context.GetChild(0).GetText() == "...") { /* 只有 ... 参数 */
            _funcState.Proto._vararg = true;                 /* 标记参数重有 vararg 参数；declared vararg */
        } else {
            paramCount               = _TranslateNamelist(context.namelist());
            _funcState.Proto._vararg = context.DOTS() != null;
        }

        _UpdateLocalVars(paramCount); /* 更新活跃变量数（不包括 vararg） */
        _funcState.Proto._paramCount = (byte)_funcState.activeLocalVarCount;
        _funcState.ReserveRegs(_funcState.activeLocalVarCount);
    }

    /* 表字面值 */
    private void _TranslateTableconstructor(LuaParser.TableconstructorContext context, ExpDesc output) {
        /* output 输出表的表达式 */
        ExpDesc table = output; /* alias */

        /* 新建表并固定在栈顶 */
        InstructionIndex instIndex = _funcState.GenerateCodeABC(OpCode.NEWTABLE, 0, 0, 0, context.Start.Line);
        table.Reset(ExpType.RELOCABLE, instIndex);
        _funcState.CloseToNextReg(table, context.Start.Line);

        if (context.fieldlist() == null) /* 无元素，直接返回 */
            return;
        else
            _TranslateFieldlist(context.fieldlist(), output);
    }
    private void _TranslateFieldlist(LuaParser.FieldlistContext context, ExpDesc table) {
        ConstructorControl ctorControl = new ConstructorControl(table);
        var                fields      = context.field();
        for (int i = 0; i < fields.Length; i++)
            _TranslateField(fields[i], table, ctorControl, i == fields.Length - 1);
    }
    private void _TranslateField(LuaParser.FieldContext context, ExpDesc table, ConstructorControl ctorControl, bool last) {
        /* close previous list field */
        ExpDesc recentElem = ctorControl.recentElem;
        LuaDebug.Assert(recentElem.Type == ExpType.VOID || ctorControl.pendingElemCount > 0);
        if (recentElem.Type != ExpType.VOID) {
            _funcState.CloseToNextReg(recentElem, context.Start.Line); /* 列表项放入寄存器，待 OP_SETLIST 指令处理 */
            recentElem.Reset();
            if (ctorControl.pendingElemCount == LuaConfig.LFIELDS_PER_FLUSH) { /* 列表项数量到达缓冲 */
                _funcState.GenerateSetList(table.Reg, ctorControl.elemCount, ctorControl.pendingElemCount, context.Start.Line);
                ctorControl.pendingElemCount = 0;
            }
        }

        /* handle field */
        void RectField(ExpDesc key, LuaParser.ExpContext valueExpContext, ExpDesc value) {
            ctorControl.recordCount++;
            ArgValue keyArg = _funcState.Store(key, context.Start.Line);
            _TranslateExp(valueExpContext, value);
            ArgValue valueArg = _funcState.Store(value, context.Start.Line);
            _funcState.GenerateCodeABC(OpCode.SETTABLE, table.Reg, (ushort)keyArg.Raw, (ushort)valueArg.Raw, context.Start.Line);
        }
        ExpDesc    key_   = new ExpDesc();
        ExpDesc    value_ = new ExpDesc();
        FrameIndex reg    = _funcState.FreeReg;
        if (context.NAME() != null) { /* NAME = exp */
            _CheckLimit(ctorControl.recordCount, int.MaxValue, "table constructor records");
            _AddName(context.NAME().Symbol, key_);
            RectField(key_, context.exp(0), value_);
            _funcState.FreeReg = reg;
        } else if (context.GetChild(0).GetText() == "[") { /* exp = exp */
            _TranslateIndexedKey(context.exp(0), key_);
            RectField(key_, context.exp(1), value_);
            _funcState.FreeReg = reg;
        } else { /* exp, list field */
            _CheckLimit(ctorControl.elemCount, int.MaxValue, "table constructor elements");
            _TranslateExp(context.exp(0), recentElem);
            ctorControl.elemCount++;
            ctorControl.pendingElemCount++;
        }

        /* handle last list field，处理一下最后的 vararg，并把把剩余 pending elem 都扔到表上 */
        if (last && ctorControl.pendingElemCount > 0) {
            if (recentElem.Type.HasMultiResults()) {
                _funcState.SetMultiResults(recentElem);
                _funcState.GenerateSetList(table.Reg, ctorControl.elemCount, LuaConst.MULTRET, context.Start.Line);
            } else {
                if (recentElem.Type != ExpType.VOID)
                    _funcState.CloseToNextReg(recentElem, context.Start.Line);
                _funcState.GenerateSetList(table.Reg, ctorControl.elemCount, ctorControl.pendingElemCount, context.Start.Line);
            }
        }
    }
    private void _TranslateFieldsep(LuaParser.FieldsepContext context) {
        /* Field separators don't generate code, they're just syntax */
    }

    /* 其他字面值 */
    private void _TranslateNumber(LuaParser.NumberContext context, ExpDesc output) {
        if (context.FLOAT() != null) {
            output.Reset(((LToken)context.FLOAT().Symbol).n);
        } else if (context.HEX_FLOAT() != null) {
            output.Reset(((LToken)context.HEX_FLOAT().Symbol).n);
        } else { /* 十进制、十六进制整型都有可能是浮点数 */
            LToken ltoken;
            if (context.INT() != null)
                ltoken = (LToken)context.INT().Symbol;
            else
                ltoken = (LToken)context.HEX().Symbol;
            if (ltoken.n == 0)
                output.Reset(ltoken.i);
            else
                output.Reset(ltoken.n);
        }
    }
    private void _TranslateString(LuaParser.StringContext context, ExpDesc output) {
        if (context.NORMALSTRING() != null)
            output.Reset(_funcState, new LuaString(((LToken)context.NORMALSTRING().Symbol).str));
        else if (context.CHARSTRING() != null)
            output.Reset(_funcState, new LuaString(((LToken)context.CHARSTRING().Symbol).str));
        else
            output.Reset(_funcState, new LuaString(((LToken)context.LONGSTRING().Symbol).str));
    }

    /* ---------------- Translation Helper ---------------- */

    /* index ::= '[' expr ']'，v 输入时是 indexed 表达式，输出时是已求值 */
    private void _TranslateIndexedKey(LuaParser.ExpContext context, ExpDesc indexedKey) {
        _TranslateExp(context, indexedKey);
        _funcState.Simplify(indexedKey, context.Start.Line);
    }
    /* fieldsel ::= ['.' | ':'] NAME，indexed 输入时是被索引表，输出时是整个索引 */
    private void _TranslateFieldSelector(ExpDesc indexed, string keyName, int line) {
        ExpDesc key = new ExpDesc();
        _funcState.Close_ExceptUpval(indexed, line);
        _AddName(keyName, key);
        _funcState.Index(indexed, key, line);
    }
    /* 对于任何没有 prefix 的变量，需要直接从上下文中查找符号（称为 “PrimaryVar”）。如果是全局变量则会转为索引表达式 VINDEXED（索引全局表） */
    private void _TranslatePrimaryVar(ExpDesc output, string name, int line) {
        LuaString varName = _NewLuaString(name);
        _AddVarHelper(_funcState, varName, output, true);
        if (output.Type == ExpType.VOID) { /* 是否是全局变量 */
            _AddVarHelper(_funcState, LuaGlobalState.env, output, true);
            LuaDebug.Assert(output.Type != ExpType.VOID);
            ExpDesc key = new ExpDesc(ExpType.K);
            _AddName(varName, key);
            _funcState.Index(output, key, line);
        }
    }
    /* 尝试寻找非全局变量 */
    private void _AddVarHelper(FuncState funcState, LuaString targetVarName, ExpDesc output_, bool searchInCurrFrame) {
        if (funcState == null)           /* 仅递归时会触发，查完整个调用链都找不到这个变量，于是解析为全局变量 */
            output_.Reset(ExpType.VOID); /* 找不到，是全局变量，待后续处理 */
        else {
            if (_SearchLocalVar(funcState, targetVarName, out FrameIndex reg)) { /* 尝试在当前层栈帧查找局部变量 */
                output_.Reset(ExpType.LOCAL, reg);
                if (!searchInCurrFrame) /* 如果变量不实在当前栈帧找到的，则说明该变量将会被用于上值 */
                    _MarkUpvalue(funcState, reg);
            } else { /* 如果栈帧内没找到局部变量，则尝试寻找上值 */
                if (!_SearchUpvalue(funcState, targetVarName.Str, out int upvalueIndex)) {
                    _AddVarHelper(funcState.Prev, targetVarName, output_, false); /* 当前栈帧找不到上值，取上一层栈帧寻找（变量或上值） */
                    if (output_.Type == ExpType.VOID)                             /* 如果都找不到，则必然是全局变量，交由后面的代码处理 */
                        return;
                    upvalueIndex = _NewUpvalue(funcState, targetVarName, output_);
                }
                output_.Reset(ExpType.UPVAL, upvalueIndex);
            }
        }
    }
    /* 把 一般表达式 转为 'false 条件表达式'，即仅在 false 时发生跳转，否则继续执行 */
    private InstructionIndex _TranslateConditionExp(LuaParser.ExpContext expContext) {
        ExpDesc exp = new ExpDesc();
        _TranslateExp(expContext, exp);
        if (exp.Type == ExpType.NIL)
            exp.Reset(ExpType.FALSE);
        _funcState.CreateFalseJump(exp, expContext.Start.Line); /* false 时跳转 */
        return exp.falseList;
    }
    private void _TranslateGotoHelper(InstructionIndex jumpInstIndex, int startLine, LuaString name) {
        int idx = _NewLabelOrGoto(_dyd.pendingGotoList, name, startLine, jumpInstIndex);
        _TryFindLabel(idx);
    }

    /* 检查并处理赋值列表中的冲突，如：b[a], a = 1, 2。此时要先备份 a；还有一种情况是 b[a], b = 1, 2，此时要备份 b */
    private void _PreventAssignConflict(LinkedList<ExpDesc> lhsList, ExpDesc target, int line) {
        FrameIndex extra     = _funcState.FreeReg; /* 如果需要备份，则从这里开始备份 */
        bool       conflict  = false;
        int        targetArg = target.Type == ExpType.LOCAL ? target.Reg : target.UpvalueIndex;
        foreach (ExpDesc lhs in lhsList.Reverse()) { /* 从右往左遍历 lhs */
            if (lhs.Type != ExpType.INDEXED)
                continue;
            /* 备份被索引对象的情况，如：b[a], b = 1, 2 */
            if (lhs.IndexInfo_.tableType == target.Type && lhs.IndexInfo_.table == targetArg) {
                conflict                    = true;
                ExpDesc.IndexInfo indexInfo = lhs.IndexInfo_;
                indexInfo.tableType         = ExpType.LOCAL /* 表的类型改为局部变量 */;
                indexInfo.table             = extra; /* 备份 */
                lhs.Reset(indexInfo);
            }
            /* 备份“键”的情况，如：b[a], a = 1, 2 */
            if (target.Type == ExpType.LOCAL && lhs.IndexInfo_.key.HasRegister &&  //
                (FrameIndex)lhs.IndexInfo_.key.RKValue == target.Reg) {
                conflict                    = true;
                ExpDesc.IndexInfo indexInfo = lhs.IndexInfo_;
                indexInfo.key               = ArgValue.FromRegister(extra);
                lhs.Reset(indexInfo);
            }
        }
        if (conflict) { /* 把值拷贝到 extra 上 */
            OpCode op = target.Type == ExpType.LOCAL ? OpCode.MOVE : OpCode.GETUPVAL;
            _funcState.GenerateCodeABC(op, extra, (ushort)targetArg, 0, line);
            _funcState.ReserveRegs(1);
        }
    }
}

/* 工具函数部分 */
internal partial class LuaCodeTranslator
{
    private class ConstructorControl
    {
        internal ExpDesc recentElem; /* 最近未处理的列表元素 */
        internal ExpDesc table;
        internal int     recordCount;      /* 键值对数量 */
        internal int     elemCount;        /* 列表项数量 */
        internal int     pendingElemCount; /* 未存入的列表元素数量（待生成 SETLIST 指令） */

        public ConstructorControl(ExpDesc table) {
            this.recentElem       = new ExpDesc();
            this.table            = table;
            this.recordCount      = 0;
            this.elemCount        = 0;
            this.pendingElemCount = 0;
        }
    }

    private void _AddName(IToken token, ExpDesc output) {
        LToken ltoken = (LToken)token;
        LuaParserUtils.Check(ltoken.Type == LuaParser.NAME, "token type is not NAME");
        output.Reset(_funcState, _NewLuaString(ltoken.str));
    }
    private void _AddName(string name, ExpDesc output) {
        output.Reset(_funcState, _NewLuaString(name));
    }
    private void _AddName(LuaString name, ExpDesc output) {
        output.Reset(_funcState, name);
    }
    /* 创建子原型 */
    private LuaProto _NewSubProto(LuaString source) {
        LuaProto currProto = _funcState.Proto;
        LuaProto subProto  = new LuaProto(source);
        currProto.GetSubProtos().Add(subProto);
        return subProto;
    }
    /* 用最新的 proto 生成闭包字节码（生成在外部函数的子函数列表中） */
    private void _GenerateClosureCode(ExpDesc outputClosureExp, int line) {
        FuncState        prevFunc  = _funcState.Prev;
        InstructionIndex instIndex = prevFunc.GenerateCodeABx(OpCode.CLOSURE, 0, (uint)(prevFunc.Proto.SubProtosCount - 1), line);
        outputClosureExp.Reset(ExpType.RELOCABLE, instIndex);
        prevFunc.CloseToNextReg(outputClosureExp, line);
    }
    /* 调整批量赋值（配平左右两侧数量），并分配/回收寄存器。若 rhs 不足，则会尝试将最右侧的变量求多返回值 */
    private void _AdjustAssignment(int lhsCount, int rhsCount, ExpDesc lastRhs, int line) {
        int lack = lhsCount - rhsCount;               /* 表达式需要的额外返回 */
        if (lastRhs.Type.HasMultiResults()) {         /* 最右侧表达式是否有多返回值 */
            lack++;                                   /* 最右侧表达式应返回的实际数量，要把自己算进去所以要 +1 */
            lack = Math.Max(lack, 0);                 /* 不能小于 0 */
            _funcState.SetResultCount(lastRhs, lack); /* 让最后一个表达式返回多返回值，如果是 vararg 这里会分配一个寄存器 */
            if (lack > 1)
                _funcState.ReserveRegs(lack - 1); /* 多返回值需要额外的寄存器 */
        } else {                                  /* 非多返回表达式 */
            if (lastRhs.Type != ExpType.VOID)
                _funcState.CloseToNextReg(lastRhs, line); /* close last expression */
            if (lack > 0) {                               /* rhs 不足，用 nil 填充 */
                FrameIndex reg = _funcState.FreeReg;
                _funcState.ReserveRegs(lack);
                _funcState.GenerateLoadNil(reg, (FrameIndex)lack, line);
            }
        }
        if (rhsCount > lhsCount) /* 如果 rhs 过多，会被释放掉，也就是说该【接口总是假设 rhs 后面不会再使用】 */
            _funcState.FreeReg -= (FrameIndex)(rhsCount - lhsCount);
    }

    private void _NewLocalVar(LuaString name) {
        int _RegisterLocalVar(LuaString name_) {
            /* 返回局部变量索引（这玩意在 lparser 里的命名非常坑爹，说是返回一个 'reg' 但其实完全不是返回寄存器的意思） */
            var localVars = _funcState.Proto.GetLocalVars();
            localVars.Add(new LocalVar(name_));
            return localVars.Count - 1;
        }
        /* 这里其实没有分配寄存器（freereg 没有增加），只是预先把对应的寄存器绑定上了，期待后续的 exp2XXX 来占用寄存器 */
        int localVarIndex = _RegisterLocalVar(name);
        int localVarCount = _dyd.activeVarList.Count - _funcState.firstActiveLocalVarIdx + 1;
        _CheckLimit(localVarCount + 1, MAX_VAR_COUNT, "local variables");
        _dyd.activeVarList.Add(localVarIndex);
    }
    private void _NewLocalVar(string name) {
        _NewLocalVar(_NewLuaString(name));
    }
    private LocalVar _GetLocalVar(FuncState funcState, FrameIndex reg) {
        int localVarIdx = _dyd.activeVarList[funcState.firstActiveLocalVarIdx + reg];
        LuaDebug.Assert(localVarIdx < funcState.Proto.LocalVarsCount);
        return funcState.Proto.LocalVars[localVarIdx];
    }
    /* 把最近刚新建的 count 个局部变量的 startPC 信息设为当前 pc */
    private void _UpdateLocalVars(FrameIndex count) {
        _funcState.activeLocalVarCount += count;
        for (; count > 0; count--)
            _GetLocalVar(_funcState, (FrameIndex)(_funcState.activeLocalVarCount - count)).startPC = _funcState.Pc;
    }
    /* 移除最近新建的 n 个局部变量，直到只剩 remained 个，并设置被移除局部变量的 endpc */
    private void _RemoveLocalVars(int remained) {
        int removeCount = _funcState.activeLocalVarCount - remained;
        while (_funcState.activeLocalVarCount > remained) {
            _funcState.activeLocalVarCount--;
            _GetLocalVar(_funcState, _funcState.activeLocalVarCount).endPC = _funcState.Pc;
        }
        CommonUtils.RemoveLastElems(_dyd.activeVarList, removeCount);
    }
    private bool _SearchLocalVar(FuncState funcState, LuaString name, out FrameIndex reg) {
        reg = 0;
        for (int i = funcState.activeLocalVarCount - 1; i >= 0; i--) {
            if (name.Equals(_GetLocalVar(funcState, (FrameIndex)i).varName)) {
                reg = (FrameIndex)i;
                return true;
            }
        }
        return false;
    }

    /* 注：LuaParser 内用的 upvalueIndex 都是从 0 开始的，而内核那边用的 Proto 接口是从 1 开始的（称为 upvalueLdx） */
    private bool _SearchUpvalue(FuncState funcState, string name, out int upvalueIndex) {
        var upvalueDescList = funcState.Proto.UpvalueDescList;
        for (int i = 0; i < funcState.Proto.UpvalueDescCount; i++) {
            if (upvalueDescList[i].Name == name) {
                upvalueIndex = i;
                return true;
            }
        }
        upvalueIndex = 0;
        return false;
    }
    private int _NewUpvalue(FuncState funcState, LuaString name, ExpDesc upvalueExp) {
        LuaDebug.AssertExpType(upvalueExp.Type, ExpType.LOCAL, ExpType.UPVAL);
        var upvalueDescList = funcState.Proto.GetUpvalueDescList();
        _CheckLimit(upvalueDescList.Count + 1, LuaConfig.MAX_UPVALUE_COUNT, "upvalues");
        bool instack = upvalueExp.Type == ExpType.LOCAL;
        int  ldx     = instack ? upvalueExp.Reg + 1 : upvalueExp.UpvalueIndex + 1;
        upvalueDescList.Add(new UpvalueDesc(name, instack, ldx));
        return upvalueDescList.Count - 1;
    }
    private void _MarkUpvalue(FuncState funcState, FrameIndex to) {
        BlockControl block = funcState.block;
        while (block.activeOutVarCount > to) /* 找到 to 所在的 block */
            block = block.prev;
        block.hasUpvalue = true;
    }

    /* 关闭（回填）goto。当 parser 在当前代码块发现一个之前 goto 语句的标签 label 时，就会调用 closegoto */
    private void _CloseGoto(int pendingGotoIndex, in LabelDesc label) {
        List<LabelDesc> pendingGotoList = _dyd.pendingGotoList;
        LabelDesc       pendingGoto     = pendingGotoList[pendingGotoIndex];
        LuaDebug.Assert(pendingGoto.name.Equals(label.name));
        if (pendingGoto.activeLocalVarCount < label.activeLocalVarCount) { /* goto 和 label 之间不能有新变量（相对于 goto） */
            LuaString varName = _GetLocalVar(_funcState, pendingGoto.activeLocalVarCount).varName;
            throw new LuaSemanticError($"<goto {pendingGoto.name}> at line {pendingGoto.line} jumps into the scope of local '{varName.Str}'"
            );
        }
        _funcState.PatchTarget(pendingGoto.instIndex, label.instIndex); /* 把跳转列表目标设为 label 的下一个指令地址 */

        /* 从 pendingGotoList 列表中移除 pendingGoto，先向前挪覆盖掉 pendingGoto */
        for (int i = pendingGotoIndex; i < pendingGotoList.Count - 1; i++)
            pendingGotoList[i] = pendingGotoList[i + 1];
        pendingGotoList.RemoveAt(pendingGotoList.Count - 1); /* 然后删除最后一个元素 */
    }
    /* 尝试从当前标签列表中找到一个能关闭 pendingGoto 的标签（若能找到则表示这是一个反向跳转；若找不到则表示标签还未创建） */
    private bool _TryFindLabel(int pendingGotoIndex) {
        BlockControl    block           = _funcState.block;
        List<LabelDesc> pendingGotoList = _dyd.pendingGotoList;
        LabelDesc       pendingGoto     = pendingGotoList[pendingGotoIndex];
        for (int i = block.firstLabelIdx; i < _dyd.labelList.Count; i++) {
            LabelDesc label = _dyd.labelList[i];
            if (label.name.Equals(pendingGoto.name)) {
                /* 如果能找到标签，则说明是反跳（label 在 goto 之前），label 和 goto 之间新建的局部变量要关闭
                   从这里的代码可以看出，即使在循环创建闭包，闭包也是能正确捕获循环内新建的变量的；
                   而 C#、Python 的表现则不同，循环创建闭包捕获的始终是最后一个创建的那个 */
                /* 生成关闭上值的代码；if 条件里还包括：如果当前块存在活跃标签，即使块内无上值，也要执行上值关闭的情况。这个我有点没看懂，可能是因为不知道从哪跳过来的 */
                if (pendingGoto.activeLocalVarCount > label.activeLocalVarCount &&
                    (block.hasUpvalue || _dyd.labelList.Count > block.firstLabelIdx))
                    /* 在 goto 标签的跳转指令的跳转列表上，对每条指令都加上关闭上值的操作，关闭所有索引 >= lb->nactvar 的上值 */
                    _funcState.PatchClose(pendingGoto.instIndex, label.activeLocalVarCount);
                _CloseGoto(pendingGotoIndex, label);
                return true;
            }
        }
        return false;
    }
    /* 查找当前块中是否有和 lb 匹配的 pending goto。若有，则执行 closegoto（注：这是一个正向跳转） */
    private void _TryFindGotos(in LabelDesc label) {
        List<LabelDesc> pendingGotoList = _dyd.pendingGotoList;
        int             i               = _funcState.block.firstGotoIdx;
        while (i < pendingGotoList.Count) {
            if (pendingGotoList[i].name.Equals(label.name))
                _CloseGoto(i, label);
            else
                i++;
        }
    }
    /* 在列表上创建新 label 条目（也适用于 goto） */
    private int _NewLabelOrGoto(List<LabelDesc> list, LuaString name, int line, int pc) {
        list.Add(new LabelDesc(name, line, pc, _funcState.activeLocalVarCount));
        return list.Count - 1;
    }
    /* 离开代码块时将块内的 pending goto “移出”到外层块，并检查一波外层块的 label 列表；
       另外，如果 goto 跳出了某些上值的作用域，那么就要关闭这些上值 */
    private void _MoveGotosOut(BlockControl block) {
        List<LabelDesc> pendingGotoList = _dyd.pendingGotoList;
        /* correct pending gotos to current block and try to close it with visible labels */
        int i = block.firstGotoIdx;
        while (i < pendingGotoList.Count) { /* 遍历当前块的 pending gotos */
            LabelDesc goto_ = pendingGotoList[i];
            if (goto_.activeLocalVarCount > block.activeOutVarCount) {
                if (block.hasUpvalue)                                                /* 如果 block 上存在上值 */
                    _funcState.PatchClose(goto_.instIndex, block.activeOutVarCount); /* 关闭上值 */
                goto_.activeLocalVarCount = block.activeOutVarCount;
                pendingGotoList[i] = goto_; /* LabelDesc 是值类型。。。危险 */
            }
            if (!_TryFindLabel(i))
                i++; /* 如果没关闭 label，就 i++。这是因为如果 closegoto 成功，会做数组元素删除，这样 i 就自动指向下一个 goto 了 */
        }
    }
    /* 离开循环块时调用，为 break 语句生成一个隐式的【退出标签】，以便将所有指向这个 break 的 pending goto 正确闭合 */
    private void _GenerateBreakLabel() {
        LuaString name  = _NewLuaString("break");
        int       index = _NewLabelOrGoto(_dyd.labelList, name, 0, _funcState.Pc);
        _TryFindGotos(_dyd.labelList[index]);
    }

    private void _OpenFunc(string funcname, int firstLine = 0, int lastLine = 0) {
        LuaString source = new LuaString($"'{funcname}' at {_srouce.Str}:{firstLine}");
        if (_funcState == null) {
            _funcState = new FuncState(_state, this, _constantsIMap, _funcState, _dyd.activeVarList.Count, source);
        } else {
            LuaProto subProto = _NewSubProto(source); /* 基于当前 funcState 生成子闭包 */
            _funcState        = new FuncState(_state, this, _constantsIMap, _funcState, _dyd.activeVarList.Count, _srouce, subProto);
        }
        _funcState.Proto._frameSize = 2; /* 栈帧至少 2 格大小 */
        _EnterBlock(false);
        _funcState.Proto._firstLine = firstLine;
        _funcState.Proto._lastLine  = lastLine;
    }
    private void _CloseFunc(int line) {
        _funcState.GenerateReturn(0, 0, line); /* 生成返回字节码，第一个元素就在栈底（funcbase 会被覆盖），B 为 0 表示自动获取 */
        _LeaveBlock(line);
        LuaDebug.Assert(_funcState.block == null);
        _funcState = _funcState.Prev;
    }
    /* 进入新块时，新建块信息，并返回 */
    private BlockControl _EnterBlock(bool loop) {
        _funcState.block = new BlockControl(
            _funcState.block, _dyd.pendingGotoList.Count, _dyd.labelList.Count, _funcState.activeLocalVarCount, false, loop
        );
        LuaDebug.Assert(_funcState.FreeReg == _funcState.activeLocalVarCount);
        return _funcState.block;
    }
    /* 离开代码块，对块内的各种资源进行清理和闭合 */
    private void _LeaveBlock(int line) {
        BlockControl leavingBlock = _funcState.block;
        if (leavingBlock.prev != null && leavingBlock.hasUpvalue) { /* 检查是否需要关闭上值 */
            InstructionIndex jump = _funcState.GeneratePendingJump(line);
            _funcState.PatchClose(jump, leavingBlock.activeOutVarCount); /* 通过 OP_JMP 指令的复合能力，关闭上值 */
            _funcState.PatchTargetToNext(jump);                          /* 直接回填跳转列表为下个指令地址 */
        }
        if (leavingBlock.loop) /* 如果是循环块，则创建 break 标签并关闭对应 label */
            _GenerateBreakLabel();

        _funcState.block = leavingBlock.prev;
        _RemoveLocalVars(leavingBlock.activeOutVarCount); /* 标记局部变量描述信息的结束 pc 位置，并恢复局部变量数量 */
        LuaDebug.Assert(leavingBlock.activeOutVarCount == _funcState.activeLocalVarCount);

        _funcState.FreeReg = _funcState.activeLocalVarCount;            /* 释放寄存器 */
        CommonUtils.Shrink(_dyd.labelList, leavingBlock.firstLabelIdx); /* 移除块内标签 */
        if (leavingBlock.prev != null)
            _MoveGotosOut(leavingBlock);
        else if (leavingBlock.firstGotoIdx < _dyd.pendingGotoList.Count)
            throw new LuaUndefinedGoto(_dyd.pendingGotoList[leavingBlock.firstGotoIdx]);
    }
    private void _EnterLevel() {
        _state.cCalls++;
        _CheckLimit(_state.cCalls, LuaConfig.LUAI_MAXCCALLS, "calls");
    }
    private void _LeaveLevel() {
        _state.cCalls--;
    }

    private void _CheckLimit(int curr, int limit, string what) {
        if (curr > limit)
            throw new LuaOverLimitError(_funcState, limit, what);
    }
    private LuaString _NewLuaString(string str) {
        /* 其实不是所有地方都要调用这个，感觉在遇到 Name 的时候调用一下就可以了，像什么字符串就不需要管 */
        if (_reservedWords.ContainsKey(str)) /* 检查一下 reserved 的字符串 */
            return _reservedWords[str];
        else
            return new LuaString(str);
    }
    private bool _IsEmptyContext(ParserRuleContext context) {
        return context == null || context.ChildCount == 0;
    }
}

}
