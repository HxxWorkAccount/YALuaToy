namespace YALuaToy.Compilation {

using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Core;

using FrameIndex       = System.Byte;  /* 栈帧索引（0 表示栈帧起点，寄存器是从 1 开始的） */
using InstructionIndex = System.Int32; /* 指令索引 */

internal enum BinaryOp {
    ADD,
    SUB,
    MUL,
    MOD,
    POW,
    DIV,
    IDIV,
    BAND,
    BOR,
    BXOR,
    SHL,
    SHR,
    CONCAT,
    EQ,
    LT,
    LE,
    NE,
    GT,
    GE,
    AND,
    OR,
    NOBINOPR
}
internal static class BinaryOpExtensions
{
    public static OpCode ToOpCode(this BinaryOp op) {
        switch (op) {
        case BinaryOp.ADD:
            return OpCode.ADD;
        case BinaryOp.SUB:
            return OpCode.SUB;
        case BinaryOp.MUL:
            return OpCode.MUL;
        case BinaryOp.MOD:
            return OpCode.MOD;
        case BinaryOp.POW:
            return OpCode.POW;
        case BinaryOp.DIV:
            return OpCode.DIV;
        case BinaryOp.IDIV:
            return OpCode.IDIV;
        case BinaryOp.BAND:
            return OpCode.BAND;
        case BinaryOp.BOR:
            return OpCode.BOR;
        case BinaryOp.BXOR:
            return OpCode.BXOR;
        case BinaryOp.SHL:
            return OpCode.SHL;
        case BinaryOp.SHR:
            return OpCode.SHR;
        case BinaryOp.CONCAT:
            return OpCode.CONCAT;
        case BinaryOp.EQ:
            return OpCode.EQ;
        case BinaryOp.LT:
        case BinaryOp.GT: /* 特殊处理，共用一个实现 */
            return OpCode.LT;
        case BinaryOp.LE:
        case BinaryOp.GE: /* 特殊处理，共用一个实现 */
            return OpCode.LE;
        }
        throw new LuaSyntaxError($"Can't convert to op: {op}");
    }
    public static Op ToOp(this BinaryOp op) {
        switch (op) {
        case BinaryOp.ADD:
            return Op.ADD;
        case BinaryOp.SUB:
            return Op.SUB;
        case BinaryOp.MUL:
            return Op.MUL;
        case BinaryOp.MOD:
            return Op.MOD;
        case BinaryOp.POW:
            return Op.POW;
        case BinaryOp.DIV:
            return Op.DIV;
        case BinaryOp.IDIV:
            return Op.IDIV;
        case BinaryOp.BAND:
            return Op.BAND;
        case BinaryOp.BOR:
            return Op.BOR;
        case BinaryOp.BXOR:
            return Op.BXOR;
        case BinaryOp.SHL:
            return Op.SHL;
        case BinaryOp.SHR:
            return Op.SHR;
        }
        throw new LuaSyntaxError($"Can't convert to op: {op}");
    }
}

internal enum UnaryOp {
    MINUS,
    BNOT,
    NOT,
    LEN,
    NOUNOPR
}
internal static class UnaryOpExtensions
{
    public static Op ToOp(this UnaryOp op) {
        switch (op) {
        case UnaryOp.MINUS:
            return Op.UNM;
        case UnaryOp.BNOT:
            return Op.BNOT;
        }
        throw new LuaSyntaxError($"Can't convert to op: {op}");
    }
    public static OpCode ToOpCode(this UnaryOp op) {
        switch (op) {
        case UnaryOp.MINUS:
            return OpCode.UNM;
        case UnaryOp.BNOT:
            return OpCode.BNOT;
        case UnaryOp.NOT:
            return OpCode.NOT;
        case UnaryOp.LEN:
            return OpCode.LEN;
        }
        throw new LuaSyntaxError($"Can't convert to op: {op}");
    }
}

internal partial class FuncState
{
    /* Lua 中跳转用的 offset 是相对于 pc+1 的偏移。如果 offset 是 -1，
       那么其实就是跳转到自己，所以这种情况就用来表示没有跳转 */
    internal const InstructionIndex NO_JUMP = -1; /* 空跳转列表 */

    /* ---------------- Generate Code ---------------- */

    public InstructionIndex GenerateCodeABC(OpCode op, byte a, ushort b, ushort c, int line) {
        LuaDebug.Assert(op.Modes().OpType == OpType.ABC);
        LuaDebug.Assert(op.Modes().BMode != OpArgMask.ArgN || b == 0);
        LuaDebug.Assert(op.Modes().CMode != OpArgMask.ArgN || c == 0);
        LuaDebug.Assert(a <= LuaOpCodes.MAXARG_A && b <= LuaOpCodes.MAXARG_B && c <= LuaOpCodes.MAXARG_C);
        return _GenerateInstruction(new Instruction(op, a, b, c), line);
    }
    public InstructionIndex GenerateCodeABx(OpCode op, byte a, uint bx, int line) {
        LuaDebug.Assert(op.Modes().OpType == OpType.ABx);
        LuaDebug.Assert(op.Modes().CMode == OpArgMask.ArgN);
        LuaDebug.Assert(a <= LuaOpCodes.MAXARG_A && bx <= LuaOpCodes.MAXARG_Bx);
        return _GenerateInstruction(new Instruction(op, a, (int)bx, false), line);
    }
    public InstructionIndex GenerateCodeAsBx(OpCode op, byte a, int sbx, int line) {
        /* 这里不能复用 GenerateCodeABx，要重新写一份 */
        LuaDebug.Assert(op.Modes().OpType == OpType.AsBx);
        LuaDebug.Assert(op.Modes().CMode == OpArgMask.ArgN);
        LuaDebug.Assert(a <= LuaOpCodes.MAXARG_A && sbx <= LuaOpCodes.MAXARG_sBx);
        return _GenerateInstruction(new Instruction(op, a, sbx, true), line);
    }

    private InstructionIndex _GenerateExtraArg(uint extraArg, int line) {
        LuaDebug.Assert(extraArg <= LuaOpCodes.MAXARG_Ax);
        return _GenerateInstruction(new Instruction(OpCode.EXTRAARG, extraArg), line);
    }
    public InstructionIndex GenerateLoadK(FrameIndex reg, uint constantsIndex, int line) {
        /* 生成一个加载常量的指令，如果输入的常量 >18 位二进制则用 OP_LOADKX */
        if (constantsIndex <= LuaOpCodes.MAXARG_Bx) {
            return GenerateCodeABx(OpCode.LOADK, reg, constantsIndex, line);
        } else {
            InstructionIndex index = GenerateCodeABx(OpCode.LOADK, reg, 0, line);
            _GenerateExtraArg(constantsIndex, line);
            return index;
        }
    }
    public InstructionIndex GenerateLoadNil(FrameIndex from, FrameIndex count, int line) {
        /* 如果上一条指令也是 loadnil，则会与上一条语句合并。比如 'local a; local b' 这种代码只生成一个指令 */
        FrameIndex last = (FrameIndex)(from + count - 1);
        if (Pc > 0 && Pc > lastTarget) { /* 没有到当前位置的跳转 */
            Instruction prev = _proto._instructions[Pc - 1];
            if (prev.OpCode == OpCode.LOADNIL) {
                FrameIndex prevFrom = (FrameIndex)prev.A.RKValue;
                FrameIndex prevLast = (FrameIndex)(prevFrom + prev.B.RKValue);
                if ((prevFrom <= from && from <= prevLast + 1) || (from <= prevFrom && prevFrom <= last + 1)) {
                    from                         = Math.Min(from, prevFrom);
                    last                         = Math.Max(last, prevLast);
                    prev                         = prev.CoplaceA(from).CoplaceB((ushort)(last - from));
                    _proto._instructions[Pc - 1] = prev;
                    return Pc - 1;
                }
            }
        }
        return GenerateCodeABC(OpCode.LOADNIL, from, (ushort)(count - 1), 0, line);
    }
    private InstructionIndex _GenerateLoadBool(byte a, bool b, bool jump, int line) {
        MarkLastTarget();
        return GenerateCodeABC(OpCode.LOADBOOL, a, CondArg(b), CondArg(jump), line);
    }

    /* 生成跳转指令，并返回指令地址，【注意！该函数不填写跳转目标】（通过 _PatchInstTarget 等函数回填） */
    public InstructionIndex GeneratePendingJump(int line) {
        /* 如果存在跳转跳到这个位置，则将它们链接在一起，patchlistaux 回填时会赋予它们相同的跳转目标 */
        InstructionIndex jpc     = this.jpc;
        this.jpc                 = NO_JUMP; /* 这里直接清掉 jpc */
        InstructionIndex newJump = GenerateCodeAsBx(OpCode.JMP, 0, NO_JUMP, line);
        JoinJumpList(ref newJump, jpc);
        return newJump;
    }
    public void GenerateJump(InstructionIndex to, int line) {
        InstructionIndex instIdx = GeneratePendingJump(line);
        PatchTarget(instIdx, to);
    }
    private InstructionIndex _CreateCondJump(OpCode op, byte a, ushort b, ushort c, int line) {
        GenerateCodeABC(op, a, b, c, line); /* 生成条件判断：if ((RK(B) <op> RK(C)) ~= A) then pc++。通过 pc++ “跳过”跳转指令 */
        return GeneratePendingJump(line);          /* 生成跳转指令，比返回该指令 */
    }

    public void GenerateReturn(FrameIndex firstResultReg, int resultCount, int line) {
        GenerateCodeABC(OpCode.RETURN, firstResultReg, (ushort)(resultCount + 1), 0, line);
    }
    public void GenerateSetList(FrameIndex table, int finalElemCount, int currStoreCount, int line) {
        /* finalElemCount 是执行 SetList 指令后的长度（即当前表长度 + 将要设置的元素数量）
           currStoreCount 是当前将要设置的元素数量（可能为 LUA_MULTRET，即表示直接设置到栈顶），
           当 currStoreCount 为 MULTRET 时，finalElemCount 就把最后一个 vararg 当一个元素即可 */
        int c = (finalElemCount - 1) / LuaConfig.LFIELDS_PER_FLUSH + 1;    /* 算出缓冲区序号，+1 是因为指令的定义上会 -1 */
        int b = (currStoreCount == LuaConst.MULTRET) ? 0 : currStoreCount; /* 设置数量，为 0 时读取到 top（虚拟机内实现） */
        LuaDebug.Assert(currStoreCount != 0 && currStoreCount <= LuaConfig.LFIELDS_PER_FLUSH);
        if (c <= LuaOpCodes.MAXARG_C)
            GenerateCodeABC(OpCode.SETLIST, table, (ushort)b, (ushort)c, line);
        else if (c <= LuaOpCodes.MAXARG_Ax) {
            GenerateCodeABC(OpCode.SETLIST, table, (ushort)b, 0, line); /* c == 0 的话下一条指令就是 extraarg */
            _GenerateExtraArg((uint)c, line);
        } else
            throw new LuaSyntaxError($"constructor too long: {c}");
        FreeReg = (FrameIndex)(table + 1); /* 这里假设从表开始，后面的寄存器直到 top 都用于保存待存入的数据 */
    }

    private InstructionIndex _GenerateInstruction(Instruction inst, int line) {
        _PatchJPCToNext();
        return _AddInstruction(inst, line);
    }
    private InstructionIndex _AddInstruction(Instruction inst, int line) {
        if (Pc >= _proto._instructions.Count)
            _proto._instructions.Add(inst);
        else
            _proto._instructions[Pc] = inst;
        if (Pc >= _proto.Lines.Count)
            _proto.Lines.Add(line);
        else
            _proto.Lines[Pc] = line;
        return Pc++;
    }

    /* ---------------- Expression Transformation ---------------- */

    /* -| 将表达式转为 RELOCABLE, NONRELOC 或 Constant，称为 "Simplify" |- */

    /* 优先尝试 Simplify_ExceptJump，如果有跳转那就不得不 Close 了 */
    public void Simplify(ExpDesc e, int line) {
        if (e.HasJump)
            Close(e, line);
        else
            Simplify_ExceptJump(e, line);
    }
    /// <summary>
    /// 对常量求值，确保后续指令（相对于当前 pc）访问 e 时，e 不是一个变量（但寄存器可能待回填）；具体来说：
    /// - 如果是 VLOCAL 类型，则其值已在寄存器上，把类型改为 VNONRELOC 即可；
    /// - 如果是上值 VUPVAL 类型，则【新增】一条读取上值的指令，然后类型改为 VRELOCABLE；
    /// - 如果是索引变量 VINDEXED 类型，则【新增】一条读取表的指令，然后类型改为 VRELOCABLE；
    /// - 如果是 VVARARG 或 VCALL 类型的表达式，则通过 luaK_setoneret 将其转为单值（会舍弃多返回值）
    /// </summary>
    public void Simplify_ExceptJump(ExpDesc e, int line) {
        InstructionIndex instIndex;
        switch (e.Type) {
        case ExpType.LOCAL:
            e.Reset(ExpType.NONRELOC, e.Reg);
            break;
        case ExpType.UPVAL:
            instIndex = GenerateCodeABC(OpCode.GETUPVAL, 0, e.UpvalueIndex, 0, line);
            e.Reset(ExpType.RELOCABLE, instIndex);
            break;
        case ExpType.INDEXED: {
            OpCode op;
            _TryFreeReg(e.IndexInfo_.key);
            if (e.IndexInfo_.tableType == ExpType.LOCAL) { /* 表在寄存器上 */
                _TryFreeReg(e.IndexInfo_.table);
                op = OpCode.GETTABLE;
            } else {
                LuaDebug.Assert(e.IndexInfo_.tableType == ExpType.UPVAL);
                op = OpCode.GETTABUP;
            }
            e.Reset(ExpType.RELOCABLE, GenerateCodeABC(op, 0, e.IndexInfo_.table, (ushort)e.IndexInfo_.key.Raw, line));
            break;
        }
        case ExpType.VARARG:
        case ExpType.CALL:
            SimplifyMultiResult(e);
            break;
        }
        /* there is one value available (somewhere) */
    }
    ///<summary>确保 VCALL 或 VVARARG 只有一个值</summary>
    ///<remarks>
    ///该函数【还会把表达式类型改为 VNONRELOC/VRELOCABLE】，这是与 SetResultCount 的根本区别
    ///用于需要将 VCALL 或 VVARARG 关联到固定寄存器的场景
    ///</remarks>
    public void SimplifyMultiResult(ExpDesc e) {
        if (e.Type == ExpType.CALL) { /* 函数表达式初始化时就是一个返回值，所以不用再设了 */
            LuaDebug.Assert(GetInstruction(e).C.RKValue == 2);
            e.Reset(ExpType.NONRELOC, GetInstruction(e).A.RKValue);
        } else if (e.Type == ExpType.VARARG) {
            Instruction inst                  = GetInstruction(e).CoplaceB(2); /* 把返回值数量改为 1 */
            _proto._instructions[e.InstIndex] = inst;
            e.Reset(ExpType.RELOCABLE, e.InstIndex);
        }
    }

    /* -| 将表达式转为 NONRELOC 或 K（常量索引），称为 "Store" |- */

    /* 将表达式 e 的值放入寄存器或常量表，并返回索引（可能分配寄存器） */
    public ArgValue Store(ExpDesc e, int line) {
        Simplify(e, line);
        switch (e.Type) { /* 常量移动到常量表 */
        case ExpType.TRUE:
            e.Reset(ExpType.K, _AddConstant(true));
            goto vk;
        case ExpType.FALSE:
            e.Reset(ExpType.K, _AddConstant(false));
            goto vk;
        case ExpType.NIL:
            e.Reset(ExpType.K, _AddNilConstant());
            goto vk;
        case ExpType.INT:
            e.Reset(ExpType.K, AddConstant(e.Int));
            goto vk;
        case ExpType.FLT:
            e.Reset(ExpType.K, AddConstant(e.Float));
            goto vk;
        case ExpType.K:
        vk:
            if (e.ConstantsIndex <= LuaOpCodes.MAX_RK_INDEX)          /* 寄存器索引是否过大？ */
                return ArgValue.FromConstantsIndex(e.ConstantsIndex); /* 索引大小合适，则直接返回 */
            break;
        }
        /* 非常量 或 常量索引太大，则直接塞到寄存器里面 */
        return ArgValue.FromRegister(Close(e, line));
    }

    /* -| 将表达式转为 NONRELOC ，称为 "Close" |- */

    /* 将表达式 e 的值（包括跳转列表里的）放入任意寄存器，并返回寄存器索引 */
    public FrameIndex Close(ExpDesc e, int line) {
        Simplify_ExceptJump(e, line);
        if (e.Type == ExpType.NONRELOC) {
            if (!e.HasJump)
                return e.Reg;
            if (e.Reg >= activeLocalVarCount) { /* 非局部变量？ */
                _CloseToReg(e, e.Reg, line);
                return e.Reg;
            }
        }
        CloseToNextReg(e, line);
        return e.Reg;
    }
    /* 将表达式 e 的值放入任意寄存器或上值 */
    public void Close_ExceptUpval(ExpDesc e, int line) {
        if (e.Type != ExpType.UPVAL || e.HasJump)
            Close(e, line);
    }
    /// <summary>将表达式 e 的值放入下一个可用寄存器，生成字节码前通常要按顺序将 ExpDesc 调用这个，将值压入栈中</summary>
    /// <remarks>如果 ExpDesc 本身就在寄存器上，则发生“移动”</remarks>
    public void CloseToNextReg(ExpDesc e, int line) {
        Simplify_ExceptJump(e, line);
        _TryFreeReg(e); /* 这里可能已经分配了一个寄存器，先释放掉，因为下面无条件执行一次分配 */
        ReserveRegs(1);
        _CloseToReg(e, (FrameIndex)(FreeReg - 1), line);
    }
    private void _CloseToReg(ExpDesc e, FrameIndex reg, int line) {
        /* 把一个表达式“规范化”，确保它在求值后够在指定寄存器中保存一个明确的值（非临时、不依赖跳转），即使这个表达式曾经生成过跳转指令 */
        _CloseToReg_ExceptJump(e, reg, line); /* 当且仅当 e 为 JMP 时，可能没有寄存器 */

        /* 在不影响原条件跳转逻辑的前提下，插入 LOADBOOL 使得条件表达式的布尔值被存在寄存器里 */
        if (e.Type == ExpType.JMP)
            JoinJumpList(ref e.trueList, e.InstIndex);
        if (e.HasJump) {
            InstructionIndex trueTarget  = NO_JUMP; /* 表达式值为 true 时的跳转目标 */
            InstructionIndex falseTarget = NO_JUMP; /* 表达式值为 false 时的跳转目标 */
            /* 如果有 TEST SET 以外的跳转指令，则意味着需要额外的 Load Bool 操作来将条件表达式的布尔值记录到寄存器上；否则只需要把 TESTSET 的赋值目标设为 reg 即可 */
            if (_AnyJumpExceptTestSet(e.trueList) || _AnyJumpExceptTestSet(e.falseList)) {
                /* 如果当前是 JMP，则利用它自身的跳转指令；其他条件表达式，则要生成一个跳转指令。这样条件表达式+跳转指令就构成了一个条件跳转 */
                InstructionIndex newJump = e.Type == ExpType.JMP ? NO_JUMP : GeneratePendingJump(line); /* 注意下面这条跳转是跳过 load bool 用的 */
                /* OP_LOADBOOL 实际上是一赋值 + 条件跳转指令 | R(A) := (Bool)B; if (C) pc++ */
                falseTarget = _GenerateLoadBool(reg, false, true, line); /* false 的 loadbool 要跳过下一条指令（即，true 的 laodbool） */
                trueTarget  = _GenerateLoadBool(reg, true, false, line);
                PatchTargetToNext(newJump);
            }
            InstructionIndex final = MarkLastTarget(); /* 获得下一条跳转指令 */
            /* 对于非 TestSet，无论 truelist 还是 falselist，都先跳到 load bool 上；对于 TestSet 直接全部跳过，赋值给 reg 即可 */
            _PatchListHelper(e.falseList, final, reg, falseTarget); /* TESTSET 指令在这里赋值给 reg，其他则在 GenerateLoadBool 里赋值 */
            _PatchListHelper(e.trueList, final, reg, trueTarget);
        }
        e.Reset(ExpType.NONRELOC, reg, true);
    }
    private void _CloseToNextReg_ExceptJump(ExpDesc e, int line) {
        if (e.Type != ExpType.NONRELOC) {
            ReserveRegs(1);
            _CloseToReg_ExceptJump(e, (byte)(FreeReg - 1), line);
        }
    }
    private void _CloseToReg_ExceptJump(ExpDesc e, FrameIndex reg, int line) {
        Instruction inst;
        Simplify_ExceptJump(e, line);
        switch (e.Type) {
        case ExpType.NIL:
            GenerateLoadNil(reg, 1, line);
            break;
        case ExpType.FALSE:
        case ExpType.TRUE:
            GenerateCodeABC(OpCode.LOADBOOL, reg, CondArg(e.Type == ExpType.TRUE), 0, line);
            break;
        case ExpType.K:
            GenerateLoadK(reg, e.ConstantsIndex, line);
            break;
        case ExpType.FLT:
            GenerateLoadK(reg, (uint)AddConstant(e.Float), line);
            break;
        case ExpType.INT:
            GenerateLoadK(reg, (uint)AddConstant(e.Int), line);
            break;
        case ExpType.RELOCABLE:
            inst                              = GetInstruction(e);
            _proto._instructions[e.InstIndex] = inst.CoplaceA(reg); /* 回填 RELOCABLE 的寄存器就在这 */
            break;
        case ExpType.NONRELOC:
            if (reg != e.Reg) /* 寄存器移动 */
                GenerateCodeABC(OpCode.MOVE, reg, e.Reg, 0, line);
            break;
        default:
            LuaDebug.Assert(e.Type == ExpType.JMP);
            return;
        }
        e.Reset(ExpType.NONRELOC, reg);
    }

    /* 尝试释放刚创建的寄存器，注意寄存器分配释放遵循 LIFO 规则 */
    private void _TryFreeReg(ExpDesc e) {
        /* 释放 NONRELOC 的寄存器 */
        if (e.Type == ExpType.NONRELOC)
            _TryFreeReg(e.Reg);
    }
    private void _TryFreeReg(ExpDesc exp1, ExpDesc exp2) {
        /* 以合适顺序释放两个 NONRELOC 的寄存器（哪个寄存器更大（越晚创建）就先释放哪个） */
        if (exp1.Type == ExpType.NONRELOC && exp2.Type == ExpType.NONRELOC && exp1.Reg <= exp2.Reg)
            (exp1, exp2) = (exp2, exp1);
        if (exp1.Type == ExpType.NONRELOC)
            _TryFreeReg(exp1.Reg);
        if (exp2.Type == ExpType.NONRELOC)
            _TryFreeReg(exp2.Reg);
    }
    private void _TryFreeReg(ArgValue arg) {
        /* 如果 arg 是不活跃的局部变量，则释放寄存器。当 arg 为局部变量时，它必须是最后一个局部变量 */
        if (!arg.IsConstantsIndex && arg.RKValue >= activeLocalVarCount) {
            FreeReg--;
            LuaDebug.Assert(arg.RKValue == FreeReg); /* 注意！寄存器的分配和释放遵循 LIFO 规则 */
        }
    }
    private void _TryFreeReg(FrameIndex reg) {
        if (reg >= activeLocalVarCount) {
            FreeReg--;
            LuaDebug.Assert(reg == FreeReg); /* 注意！寄存器的分配和释放遵循 LIFO 规则 */
        }
    }

    /* ---------------- Expression Action ---------------- */
    /* 表达式的动作，生成一个行为的代码，并把表达式转为结果 */

    ///<summary>生成指令，将表达式 e 的值存储到变量 var 中</summary>
    ///<remarks>e 的寄存器会被释放，然后转用 var 的寄存器</remarks>
    public void AssignToVar(ExpDesc var, ExpDesc e, int line) {
        switch (var.Type) {
        case ExpType.LOCAL: {
            _TryFreeReg(e);
            _CloseToReg(e, var.Reg, line);
            return;
        }
        case ExpType.UPVAL: {
            FrameIndex reg = Close(e, line);
            GenerateCodeABC(OpCode.SETUPVAL, reg, var.UpvalueIndex, 0, line);
            break;
        }
        case ExpType.INDEXED: {
            OpCode   op  = var.IndexInfo_.tableType == ExpType.LOCAL ? OpCode.SETTABLE : OpCode.SETTABUP;
            ArgValue arg = Store(e, line);
            GenerateCodeABC(op, var.IndexInfo_.table, (ushort)var.IndexInfo_.key.Raw, (ushort)arg.Raw, line);
            break;
        }
        default:
            LuaDebug.Assert(false, $"Can't store type: {var.Type}");
            break;
        }
        _TryFreeReg(e);
    }
    /* 把表达式 `indexed` 转为索引表达式 `indexed[key]`。调用时，t 必须是存放在寄存器或上值的表 */
    public void Index(ExpDesc indexed, ExpDesc key, int line) {
        LuaDebug.Assert(!indexed.HasJump && (indexed.Type.IsInReg() || indexed.Type == ExpType.UPVAL));
        byte    table = indexed.Type == ExpType.UPVAL ? (byte)indexed.UpvalueIndex : indexed.Reg;
        ExpType type  = indexed.Type == ExpType.UPVAL ? ExpType.UPVAL : ExpType.LOCAL;
        indexed.Reset(new ExpDesc.IndexInfo(table, type, Store(key, line)));
    }
    public void CreateSelf(ExpDesc e, ExpDesc key, int line) {
        /* 生成 self 指令，将表达式 'e' 转为 'e:key(e,' */
        Close(e, line);
        FrameIndex reg = e.Reg;
        _TryFreeReg(e);
        e.Reset(ExpType.NONRELOC, FreeReg, false); /* SELF 指令的 R(A)（即，被索引*/
        ReserveRegs(2);                            /* 这里分配两个，是因为 R(A+1) 还要占用一个（即 self 变量，会被被索引变量覆盖） */
        /* SELF | R(A+1) := R(B); R(A) := R(B)[RK(C)] */
        GenerateCodeABC(OpCode.SELF, e.Reg, reg, (ushort)Store(key, line).Raw, line);
        _TryFreeReg(key); /* 释放 key 的寄存器，其索引出的对象已放到 reg 的位置 */
    }

    /* 使得当 e 值为 false 时，跳转到 e.falselist，并把 e.t 设为 go through。必要时生成指令 */
    public void CreateFalseJump(ExpDesc e, int line) {
        InstructionIndex jumpIndex; /* 新增的跳转指令地址 */
        Simplify_ExceptJump(e, line);
        switch (e.Type) {
        case ExpType.JMP: {
            _NegateCondition(e); /* 这里 e 不能为 OP_TEST 或 OP_TESTSET */
            jumpIndex = e.InstIndex;
            break;
        }
        case ExpType.K:
        case ExpType.FLT:
        case ExpType.INT:
        case ExpType.TRUE: {
            jumpIndex = NO_JUMP; /* 一定为 true，永不跳转 */
            break;
        }
        default: {
            jumpIndex = _CreateCondJump(e, false, line); /* jump when false */
            break;
        }
        }
        JoinJumpList(ref e.falseList, jumpIndex); /* insert new jump in false list */
        PatchTargetToNext(e.trueList);
        e.trueList = NO_JUMP;
    }
    /* 使得当 e 值为 true 时，跳转到 e.truelist，并把 e.f 设为 go through。必要时生成指令 */
    public void CreateTrueJump(ExpDesc e, int line) {
        InstructionIndex jumpIndex; /* 新增的跳转指令地址 */
        Simplify_ExceptJump(e, line);
        switch (e.Type) {
        case ExpType.JMP: {
            jumpIndex = e.InstIndex;
            break;
        }
        case ExpType.NIL:
        case ExpType.FALSE: {
            jumpIndex = NO_JUMP; /* 一定为 false，永不跳转 */
            break;
        }
        default: {
            jumpIndex = _CreateCondJump(e, true, line); /* jump when false */
            break;
        }
        }
        JoinJumpList(ref e.trueList, jumpIndex); /* insert new jump in false list */
        PatchTargetToNext(e.falseList);
        e.falseList = NO_JUMP;
    }
    private InstructionIndex _CreateCondJump(ExpDesc e, bool condition, int line) {
        /* 生成条件跳转，当 e == cond 时，才执行跳转。返回跳转指令的位置 */
        if (e.Type == ExpType.RELOCABLE) {
            InstructionIndex instIndex = e.InstIndex;
            Instruction      inst      = GetInstruction(e);
            if (inst.OpCode == OpCode.NOT) {
                Pc--; /* 移除 e 的指令，即 OP_NOT，改为条件跳转（取反条件） */
                return _CreateCondJump(OpCode.TEST, (byte)inst.B.Raw, 0, CondArg(!condition), line);
            }
        }
        _CloseToNextReg_ExceptJump(e, line);
        _TryFreeReg(e);
        return _CreateCondJump(OpCode.TESTSET, ArgValue.NO_REG, e.Reg, CondArg(condition), line);
    }

    /* 设置表达式 e 的返回值数量为 nresults。对于固定数量，e 只能是 VCALL 或 VVARARG；如果非固定数量，则 e 随意 */
    public void SetResultCount(ExpDesc e, int resultCount) {
        if (e.Type == ExpType.CALL) {
            Instruction inst                  = _proto._instructions[e.InstIndex];
            _proto._instructions[e.InstIndex] = inst.CoplaceC((ushort)(resultCount + 1));
        } else if (e.Type == ExpType.VARARG) {
            Instruction inst                  = _proto._instructions[e.InstIndex];
            inst                              = inst.CoplaceB((ushort)(resultCount + 1)).CoplaceA(FreeReg);
            _proto._instructions[e.InstIndex] = inst;
            ReserveRegs(1); /* 如果是 vararg，则分配一个变量。其他寄存器的分配由 caller 决定 */
        } else {
            LuaDebug.Assert(resultCount == LuaConst.MULTRET);
        }
    }
    public void SetMultiResults(ExpDesc e) {
        SetResultCount(e, LuaConst.MULTRET);
    }

    /* 将前缀运算符 op 应用于表达式 v */
    public void ApplyUnaryOp(UnaryOp op, ExpDesc v, int line) {
        switch (op) {
        case UnaryOp.MINUS:
        case UnaryOp.BNOT:
            if (!_FoldConstant(op.ToOp(), v, ExpDesc.zero))
                _CreateUnaryOperation(op.ToOpCode(), v, line);
            break;
        case UnaryOp.LEN:
            _CreateUnaryOperation(op.ToOpCode(), v, line);
            break;
        case UnaryOp.NOT:
            _Negate(v, line);
            break;
        default:
            LuaDebug.Assert(false);
            break;
        }
    }
    /* 处理二元运算符的第一个操作数表达式 v，发生在在读取第二个操作数表达式之前。这里会对 and、or 这些有序运算加跳转代码，以便特定情况跳过第二个运算符 */
    public void PrepareBinaryOp(BinaryOp op, ExpDesc lhs, int line) {
        switch (op) {
        case BinaryOp.AND: {
            CreateFalseJump(lhs, line);
            break;
        }
        case BinaryOp.OR: {
            CreateTrueJump(lhs, line);
            break;
        }
        case BinaryOp.CONCAT: {
            CloseToNextReg(lhs, line); /* 参考 OP_CONCAT，这里操作数必须在栈上 */
            break;
        }
        case BinaryOp.ADD:
        case BinaryOp.SUB:
        case BinaryOp.MUL:
        case BinaryOp.DIV:
        case BinaryOp.IDIV:
        case BinaryOp.MOD:
        case BinaryOp.POW:
        case BinaryOp.BAND:
        case BinaryOp.BOR:
        case BinaryOp.BXOR:
        case BinaryOp.SHL:
        case BinaryOp.SHR: {
            /* 如果表达式 v 是常量且能转为数值，那就先不执行 luaK_exp2RK（因为可能可以与第二个操作数进行常量折叠） */
            if (!lhs.TryGetNumber(out _))
                Store(lhs, line);
            break;
        }
        default: {
            Store(lhs, line);
            break;
        }
        }
    }
    /* PrepareBinaryOp 后，读取第二个操作数，并执行二元运算 */
    public void ApplyBinaryOp(BinaryOp op, ExpDesc lhs, ExpDesc rhs, int line) {
        /* 注：对于 and，如果 lhs 为 true，则会通过 lhs.truelist 跳转走，后续指令不会执行；or 同理 */
        switch (op) {
        case BinaryOp.AND: {
            LuaDebug.Assert(lhs.trueList == NO_JUMP); /* truelist 再 prepare 里关闭了 */
            Simplify_ExceptJump(rhs, line);
            JoinJumpList(ref rhs.falseList, lhs.falseList);
            lhs.CopyFrom(rhs); /* lhs 的 true 已经关闭了，false 和 rhs 一样，直接用 rhs 代替 lhs */
            break;
        }
        case BinaryOp.OR: {
            LuaDebug.Assert(lhs.falseList == NO_JUMP); /* false 跳转列表已经回填并关闭了；list closed by 'luK_infix' */
            Simplify_ExceptJump(rhs, line);
            JoinJumpList(ref rhs.trueList, lhs.trueList);
            lhs.CopyFrom(rhs);
            break;
        }
        case BinaryOp.CONCAT: { /* 连接运算符是右结合，所以先对 rhs 求值，然后再合到 lhs 上 */
            /* R(A) := R(B).. ... ..R(C) */
            Simplify(rhs, line);

            if (rhs.Type == ExpType.RELOCABLE && GetInstruction(rhs).OpCode == OpCode.CONCAT) { /* 连续连接合并 */
                Instruction rhsInst = GetInstruction(rhs);
                /* 这里要求 rhs 必须是可回填的，否则没法和 lhs 合并（因为赋值目标可能不同） */
                LuaDebug.Assert(lhs.Reg == rhsInst.B.RKValue - 1);
                _TryFreeReg(lhs);
                _proto._instructions[rhs.InstIndex] = rhsInst.CoplaceB(lhs.Reg); /* rhs concat 的起点设为 lhs 的寄存器 */
                lhs.Reset(ExpType.RELOCABLE, rhs.InstIndex, false);              /* lhs 的结果与 rh*/
            } else {
                /* 无法合并，则新建寄存器和指令 */
                CloseToNextReg(rhs, line); /* operand must be on the 'stack' */
                _CreateBinaryOperation(OpCode.CONCAT, lhs, rhs, line);
            }
            break;
        }
        case BinaryOp.ADD:
        case BinaryOp.SUB:
        case BinaryOp.MUL:
        case BinaryOp.DIV:
        case BinaryOp.IDIV:
        case BinaryOp.MOD:
        case BinaryOp.POW:
        case BinaryOp.BAND:
        case BinaryOp.BOR:
        case BinaryOp.BXOR:
        case BinaryOp.SHL:
        case BinaryOp.SHR: {
            if (!_FoldConstant(op.ToOp(), lhs, rhs))
                _CreateBinaryOperation(op.ToOpCode(), lhs, rhs, line);
            break;
        }
        case BinaryOp.EQ:
        case BinaryOp.LT:
        case BinaryOp.LE:
        case BinaryOp.NE:
        case BinaryOp.GT:
        case BinaryOp.GE: {
            _Compare(op, lhs, rhs, line);
            break;
        }
        default:
            LuaDebug.Assert(false);
            break;
        }
    }
    private void _CreateUnaryOperation(OpCode op, ExpDesc e, int line) {
        /* 生成一元表达式的代码，除了 not（not 用 GenerateNot），e 会修改为结果的表达式 */
        FrameIndex reg = Close(e, line);
        _TryFreeReg(e);
        e.Reset(ExpType.RELOCABLE, GenerateCodeABC(op, 0, reg, 0, line));
        ChangeCurrInstLine(line);
    }
    private void _CreateBinaryOperation(OpCode op, ExpDesc lhs, ExpDesc rhs, int line) {
        /* 修改会输出到 lhs */
        /* 这里因为 Store 可能会释放寄存器，而释放顺序必须符合 LIFO，所以 Store 必须先调用 rhs */
        ArgValue rhsArg = Store(rhs, line);
        ArgValue lhsArg = Store(lhs, line);
        _TryFreeReg(lhs, rhs);
        lhs.Reset(ExpType.RELOCABLE, GenerateCodeABC(op, 0, (ushort)lhsArg.Raw, (ushort)rhsArg.Raw, line));
        ChangeCurrInstLine(line);
    }
    /* 生成一个返回指令 */
    private void _Compare(BinaryOp op, ExpDesc e1, ExpDesc e2, int line) {
        /* 生成比较表达式的字节码，这里假设 e1 已经被 PrepareBinaryOp 执行了 Store */
        LuaDebug.AssertExpType(e1, ExpType.K, ExpType.NONRELOC);
        ArgValue arg1;
        if (e1.Type == ExpType.K)
            arg1 = ArgValue.FromConstantsIndex(e1.ConstantsIndex);
        else
            arg1 = ArgValue.FromRegister(e1.Reg);
        ArgValue arg2 = Store(e2, line);
        _TryFreeReg(e1, e2); /* 释放 rk1 和 rk2（当前指令只是读取，下一条指令可以覆盖） */

        switch (op) {
        case BinaryOp.NE: { /* '(a ~= b)' ==> 'not (a == b)' */
            e1.Reset(ExpType.JMP, _CreateCondJump(OpCode.EQ, 0, (ushort)arg1.Raw, (ushort)arg2.Raw, line));
            break;
        }
        case BinaryOp.GT:
        case BinaryOp.GE: { /* '(a > b)' ==> '(b < a)';  '(a >= b)' ==> '(b <= a)' */
            e1.Reset(ExpType.JMP, _CreateCondJump(op.ToOpCode(), 1, (ushort)arg2.Raw, (ushort)arg1.Raw, line));
            break;
        }
        default: { /* '==', '<', '<=' use their own opcodes */
            e1.Reset(ExpType.JMP, _CreateCondJump(op.ToOpCode(), 1, (ushort)arg1.Raw, (ushort)arg2.Raw, line));
            break;
        }
        }
    }
    private void _Negate(ExpDesc e, int line) {
        /* 生成逻辑取反（not）表达式的代码，它将表达式的值取反 */
        Simplify_ExceptJump(e, line);

        /* 修正表达式类型 */
        switch (e.Type) {
        case ExpType.NIL:
        case ExpType.FALSE: {
            e.Reset(ExpType.TRUE, false);
            break;
        }
        case ExpType.K:
        case ExpType.FLT:
        case ExpType.INT:
        case ExpType.TRUE: {
            e.Reset(ExpType.FALSE, false);
            break;
        }
        case ExpType.JMP: {
            _NegateCondition(e); /* 反转跳转条件 */
            break;
        }
        case ExpType.RELOCABLE:
        case ExpType.NONRELOC: { /* 寄存器中的值 */
            _CloseToNextReg_ExceptJump(e, line);
            //     /* 很多地方都有这种 freeexp 后再使用寄存器的用法，我猜测是因为下条指令用完后，寄存器就失效了。
            //     ** 所以这里就先释放，但是寄存器的值还在，下一条语句用完后，后面生成的代码就可能把释放的寄存器覆盖掉了 */
            _TryFreeReg(e);
            e.Reset(ExpType.RELOCABLE, GenerateCodeABC(OpCode.NOT, 0, e.Reg, 0, line));
            break;
        }
        default:
            LuaDebug.Assert(false);
            break;
        }
        (e.trueList, e.falseList) = (e.falseList, e.trueList); /* 交换 e->f 和 e->t 跳转列表 */
        /* 取反后 TEST SET 就没用了，因为表达式内部不可能发生赋值 */
        _RemoveTestSet(e.falseList);
        _RemoveTestSet(e.trueList);
    }

    /* ---------------- Utils ---------------- */
    /* 以下工具最多只对 Exp 读写，不会生成代码 或 分配/回收寄存器（除了 ReserveReg 和 CheckStack） */

    public Instruction GetInstruction(ExpDesc e) {
        return _proto.GetInstruction(e.InstIndex);
    }
    public void SetInstruction(ExpDesc e, Instruction inst) {
        _proto._instructions[e.InstIndex] = inst;
    }
    private InstructionIndex _GetJumpTarget(InstructionIndex instIndex) {
        /* 获得跳转指令的跳转目标 */
        int offset = _proto._instructions[instIndex].sBx;
        if (offset == NO_JUMP)
            return NO_JUMP;
        return instIndex + offset + 1; /* JMP 指令的 offset 是相对【下一条】指令偏移 */
    }
    private InstructionIndex _GetJumpControl(InstructionIndex instIndex) {
        /* 获得跳转指令的控制语句，如果不是跳转语句，则直接返回对应指令 */
        if (instIndex <= 0)
            return instIndex;
        Instruction prevInst = _proto._instructions[instIndex - 1];
        if (prevInst.Modes.Conditional)
            return instIndex - 1;
        else
            return instIndex;
    }

    /* 提示：逻辑操作符总是生成 TESTSET，但如果编译到后边发现只是作为【条件表达式】而不是【布尔值】，就会被转为 TEST 指令 */

    /* 回填跳转列表 list，将跳转目标设为 target */
    public void PatchTarget(InstructionIndex list, InstructionIndex target) {
        if (target == Pc) { /* 目标就是下个指令？ */
            PatchTargetToNext(list);
        } else {
            LuaDebug.Assert(target < Pc); /* 只能回跳，因为只知道已创建的指令的地址 */
            _PatchListHelper(list, target, ArgValue.NO_REG, target);
        }
    }
    /* 将跳转列表 list 连接到 jpc 上（一个【持续维护的】以 pc（即，下一条指令地址）为目标的跳转链表） */
    public void PatchTargetToNext(InstructionIndex list) {
        MarkLastTarget();
        JoinJumpList(ref jpc, list);
    }
    /* 回填跳转列表 list 的上值关闭位置，具体参考 OpCode.JMP 的虚拟机逻辑 */
    public void PatchClose(InstructionIndex list, FrameIndex closeTo) {
        closeTo++; /* JMP 指令中，关闭的是 A-1 而不是 A，所以这里再 +1。之所以这样是因为 【0 表示不关闭】 */
        for (; list != NO_JUMP; list = _GetJumpTarget(list)) {
            Instruction inst = _proto._instructions[list];
            /* patch close 时，原指令要么不关闭，要么关闭位置比 closeTo 更靠后 */
            LuaDebug.Assert(inst.OpCode == OpCode.JMP && (inst.A.RKValue == 0 || inst.A.RKValue >= closeTo));
            _proto._instructions[list] = inst.CoplaceA(closeTo);
        }
    }
    /* 将跳转列表 list2 连接（尾插）到跳转列表 list1 */
    public void JoinJumpList(ref InstructionIndex list1, InstructionIndex list2) {
        if (list2 == NO_JUMP)
            return;
        else if (list1 == NO_JUMP) /* no original list? */
            list1 = list2;
        else {
            InstructionIndex last = list1;
            InstructionIndex next = _GetJumpTarget(last);
            while (next != NO_JUMP) { /* 通过遍历找到 list1 的最后一个节点 */
                last = next;
                next = _GetJumpTarget(last);
            }
            _PatchInstTarget(last, list2); /* 最后一个节点指向 list2 开头，也就是尾插 */
        }
    }
    /* 把当前指令【下个地址】标记为最近跳转目标，调用后通常会再生成一个指令（这样生成的指令就被标记为跳转目标了） */
    public InstructionIndex MarkLastTarget() {
        this.lastTarget = this.Pc;
        return this.Pc;
    }
    private void _PatchInstTarget(InstructionIndex instIndex, InstructionIndex target) {
        /* 回填【单个】指令 */
        LuaDebug.Assert(target != NO_JUMP);
        Instruction inst   = _proto._instructions[instIndex];
        int         offset = target - (instIndex + 1); /* 相对下一条指令的偏移 */
        if (Math.Abs(offset) > LuaOpCodes.MAXARG_sBx)
            throw new LuaSyntaxError($"control structure too long: {offset}");
        _proto._instructions[instIndex] = inst.CoplaceSBx(offset);
    }
    private bool _PatchTestSetAssignment(InstructionIndex instIndex, FrameIndex reg) {
        /* 回填 TEST SET 指令。如果 reg 是有效的，则把指令改为简单 TEST；否则使用 reg 回填。
           返回指令是否为 TEST SET */
        InstructionIndex controlInstIndex = _GetJumpControl(instIndex);
        Instruction      controlInst      = _proto._instructions[controlInstIndex];
        if (controlInst.OpCode != OpCode.TESTSET)
            return false;
        /* reg 存在且比较有意义（与自身比较一定为 true，无意义） */
        if (reg != ArgValue.NO_REG && reg != controlInst.B.RKValue) {
            _proto._instructions[controlInstIndex] = controlInst.CoplaceA(reg);
        } else { /* 赋值目标不存在，改为 OP_TEST */
            controlInst                            = new Instruction(OpCode.TEST, (byte)controlInst.B.Raw, 0, (ushort)controlInst.C.Raw);
            _proto._instructions[controlInstIndex] = controlInst;
        }
        return true;
    }
    private void _RemoveTestSet(InstructionIndex list) {
        /* 当确定某个条件表达式只用于跳转时，就用该函数删除跳转链上 */
        for (; list != NO_JUMP; list = _GetJumpTarget(list))
            _PatchTestSetAssignment(list, ArgValue.NO_REG);
    }
    private void _PatchListHelper(
        InstructionIndex list,
        InstructionIndex testSetJumpTarget,   /* TESTSET 条件判断的跳转目标 */
        FrameIndex       testSetAssignTarget, /* TESTSET 条件判断的赋值目标 */
        InstructionIndex testJumpTarget       /* 普通条件判断的跳转目标 */
    ) {
        while (list != NO_JUMP) {
            InstructionIndex next = _GetJumpTarget(list);
            if (_PatchTestSetAssignment(list, testSetAssignTarget)) {
                _PatchInstTarget(list, testSetJumpTarget);
            } else {
                _PatchInstTarget(list, testJumpTarget);
            }
            list = next;
        }
    }
    private void _PatchJPCToNext() {
        /* 回填 jpc 将其跳转目标设为当前 pc，将其赋值目标设为 NO_REG */
        _PatchListHelper(jpc, Pc, ArgValue.NO_REG, Pc);
        jpc = NO_JUMP;
    }
    private bool _AnyJumpExceptTestSet(InstructionIndex list) {
        /* 检查跳转列表中是否存在【不产生值的跳转指令（即 TESTSET 以外的跳转指令，包括 EQ、LT、LE、TEST）】，空列表返回 false；
           如果返回 true，则说明可能需要【额外的代码】来载入布尔值；若返回 false 则可以直接用 Test Set 来载入布尔值 */
        for (; list != NO_JUMP; list = _GetJumpTarget(list)) {
            Instruction inst = _proto._instructions[_GetJumpControl(list)];
            if (inst.OpCode != OpCode.TESTSET)
                return true;
        }
        return false;
    }
    /* 【比较】表达式条件取反 */
    private void _NegateCondition(ExpDesc e) {
        InstructionIndex instIndex = _GetJumpControl(e.InstIndex);
        Instruction      inst      = _proto._instructions[instIndex];
        LuaDebug.Assert(inst.Modes.Conditional && inst.OpCode != OpCode.TESTSET && inst.OpCode != OpCode.TEST);
        _proto._instructions[instIndex] = inst.CoplaceA(Not(CondArg(inst.A.RKValue)));
    }

    /* 将字符串常量 s 加入常量表，并返回其索引 */
    public int AddConstant(LuaString s) {
        LuaValue strValue = new LuaValue(s);
        return _AddConstant(strValue, strValue); /* 字符串用自己作为键值 */
    }
    /* 将整数常量 n 加入常量表，并返回其索引 */
    public int AddConstant(long i) {
        /* 整型常量用等价的 light userdata 作为索引，以防止整型和浮点数缓存冲突 */
        LuaValue key   = new LuaValue((IntPtr)i);
        LuaValue value = new LuaValue(i);
        return _AddConstant(key, value);
    }
    public int AddConstant(double n) {
        LuaValue value = new LuaValue(n);
        return _AddConstant(value, value);
    }
    private int _AddConstant(bool b) {
        LuaValue value = b ? LuaValue.TRUE : LuaValue.FALSE;
        return _AddConstant(value, value);
    }
    private int _AddNilConstant() {
        /* 由于 nil 无法作为键，这里直接拿全局统一的常量表映射表作为键。。。 */
        LuaValue key = new LuaValue(_constantsIMap);
        return _AddConstant(key, LuaValue.NIL);
    }
    private int _AddConstant(in LuaValue key, in LuaValue value) {
        /* 这里 key 是 _constantsIMap 的索引，value 是待加入常量数组的常量 */
        List<LuaValue> constants = _proto.GetConstants();

        /* 检查常量是否已缓存 */
        LuaValue idxValue = _constantsIMap.Get(key); /* 尝试获取缓存的常量索引 */              //
        if (idxValue.IsInt) { /* 是否有映射缓存？ */                                           //
            int constantsIndex = (int)idxValue.Int; /* 取得缓存的索引 */                       //
            if (constantsIndex >= 0 && constantsIndex < constants.Count) { /* 索引是否有效 */  //
                LuaValue oldValue = constants[constantsIndex];
                if (oldValue.Type.Raw == value.Type.Raw && oldValue.Equals(value)) /* 常量是否相同，这里要区分整型和浮点，所以比较了一次类型 */
                    return constantsIndex;                                         /* 确认是已缓存的常量，直接返回 */
            }
        }

        /* 没缓存，添加常量到常量表 */
        constants.Add(value);
        _constantsIMap.Set(key, new LuaValue(constants.Count - 1)); /* 添加常量到映射表 */
        return constants.Count - 1;                                 /* 返回常量表索引 */
    }

    private bool _FoldConstant(Op op, ExpDesc e1, ExpDesc e2) {
        if (!e1.TryGetNumber(out LuaValue v1) || !e2.TryGetNumber(out LuaValue v2) || !_CanFold(op, v1, v2))
            return false;
        _state.Arith(op, v1, v2, out LuaValue result);
        if (result.IsInt)
            e1.Reset(result.Int);
        else {
            double n = result.Float;
            if (double.IsNaN(n) || n == 0) /* 折叠结果不能是 nan 或 0.0 */
                return false;
            e1.Reset(n);
        }
        return true;
    }
    private bool _CanFold(Op op, in LuaValue v1, in LuaValue v2) {
        /* 判断是否可以常量折叠 */
        switch (op) {
        case Op.BAND:
        case Op.BOR:
        case Op.BXOR:
        case Op.SHL:
        case Op.SHR:
        case Op.BNOT: {
            /* conversion errors */
            return v1.ToInteger(out _) && v2.ToInteger(out _);
        }
        case Op.DIV:
        case Op.IDIV:
        case Op.MOD:
            return v2.Number != 0; /* division by 0 */
        }
        return true;
    }

    public void ChangeCurrInstLine(int line) {
        _proto.Lines[Pc - 1] = line;
    }
    public void ReserveRegs(int count) {
        CheckStack(count);
        FreeReg += (FrameIndex)count;
    }
    /* 检查栈空间是否足够容纳 count 个寄存器。若不足更新 proto 的 framesize */
    public void CheckStack(int count) {
        int newStackSize = FreeReg + count;
        if (newStackSize > _proto.FrameSize) {
            if (newStackSize > LuaConfig.MAX_REG_COUNT)
                throw new LuaSyntaxError("Function or expression needs too many registers");
            _proto._frameSize = (byte)newStackSize;
        }
    }

    private static byte CondArg(bool condition) {
        return condition ? (byte)1 : (byte)0;
    }
    private static byte CondArg(int condition) {
        return condition != 0 ? (byte)1 : (byte)0;
    }
    private static byte Not(byte condRawArg) {
        return condRawArg != 0 ? (byte)0 : (byte)1;
    }
}
}
