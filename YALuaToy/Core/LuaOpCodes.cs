using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using YALuaToy.Debug;

namespace YALuaToy.Core {

using RawInst    = UInt32;
using FrameIndex = Byte; /* 栈帧索引（0 表示栈帧起点，寄存器应该是从 1 开始的） */

/*===========================================================================
  We assume that instructions are unsigned numbers.
  All instructions have an opcode in the first 6 bits.
  Instructions can have the following fields:
    'A' : 8 bits
    'B' : 9 bits
    'C' : 9 bits
    'Ax' : 26 bits ('A', 'B', and 'C' together)
    'Bx' : 18 bits ('B' and 'C' together)
    'sBx' : signed Bx

  A signed argument is represented in excess K; that is, the number
  value is the unsigned value minus K. K is exactly the maximum value
  for that argument (so that -max is represented by 0, and +max is
  represented by 2*max), which is half the maximum for the corresponding
  unsigned argument.
===========================================================================*/

internal enum OpType {
    ABC,
    ABx,
    AsBx,
    Ax,
}

internal enum OpArgMask {
    ArgN, /* argument is not used */
    ArgU, /* argument is used */
    ArgR, /* argument is a register or a jump offset */
    ArgK, /* argument is a constant or register/constant */
}

internal enum OpCode {
    MOVE,      // A B     R(A) := R(B)
    LOADK,     // A Bx    R(A) := Kst(Bx)
    LOADKX,    // A       R(A) := Kst(extra arg)
    LOADBOOL,  // A B C   R(A) := (Bool)B; if (C) pc++
    LOADNIL,   // A B     R(A), R(A+1), ..., R(A+B) := nil
    GETUPVAL,  // A B     R(A) := Upvalue[B]
    GETTABUP,  // A B C   R(A) := Upvalue[B][RK(C)]
    GETTABLE,  // A B C   R(A) := R(B)[RK(C)]
    SETTABUP,  // A B C   Upvalue[A][RK(B)] := RK(C)
    SETUPVAL,  // A B     Upvalue[B] := R(A)
    SETTABLE,  // A B C   R(A)[RK(B)] := RK(C)
    NEWTABLE,  // A B C   R(A) := {} (size = B,C)
    SELF,      // A B C   R(A+1) := R(B); R(A) := R(B)[RK(C)]
    ADD,       // A B C   R(A) := RK(B) + RK(C)
    SUB,       // A B C   R(A) := RK(B) - RK(C)
    MUL,       // A B C   R(A) := RK(B) * RK(C)
    MOD,       // A B C   R(A) := RK(B) % RK(C)
    POW,       // A B C   R(A) := RK(B) ^ RK(C)
    DIV,       // A B C   R(A) := RK(B) / RK(C)
    IDIV,      // A B C   R(A) := RK(B) // RK(C)
    BAND,      // A B C   R(A) := RK(B) & RK(C)
    BOR,       // A B C   R(A) := RK(B) | RK(C)
    BXOR,      // A B C   R(A) := RK(B) ~ RK(C)
    SHL,       // A B C   R(A) := RK(B) << RK(C)
    SHR,       // A B C   R(A) := RK(B) >> RK(C)
    UNM,       // A B     R(A) := -R(B)
    BNOT,      // A B     R(A) := ~R(B)
    NOT,       // A B     R(A) := not R(B)
    LEN,       // A B     R(A) := length of R(B)
    CONCAT,    // A B C   R(A) := R(B).. ... ..R(C)
    JMP,       // A sBx   pc+=sBx; if (A) close all upvalues >= R(A - 1)
    EQ,        // A B C   if ((RK(B) == RK(C)) ~= A) then pc++
    LT,        // A B C   if ((RK(B) <  RK(C)) ~= A) then pc++
    LE,        // A B C   if ((RK(B) <= RK(C)) ~= A) then pc++
    TEST,      // A C     if not (R(A) <=> C) then pc++
    TESTSET,   // A B C   if (R(B) <=> C) then R(A) := R(B) else pc++
    CALL,      // A B C   R(A), ... ,R(A+C-2) := R(A)(R(A+1), ... ,R(A+B-1))
    TAILCALL,  // A B C   return R(A)(R(A+1), ... ,R(A+B-1))
    RETURN,    // A B     return R(A), ... ,R(A+B-2)
    FORLOOP,   // A sBx   R(A)+=R(A+2); if R(A) <?= R(A+1) then { pc+=sBx; R(A+3)=R(A) }
    FORPREP,   // A sBx   R(A)-=R(A+2); pc+=sBx
    TFORCALL,  // A C     R(A+3), ... ,R(A+2+C) := R(A)(R(A+1), R(A+2));
    TFORLOOP,  // A sBx   if R(A+1) ~= nil then { R(A)=R(A+1); pc += sBx }
    SETLIST,   // A B C   R(A)[(C-1)*FPF+i] := R(A+i), 1 <= i <= B
    CLOSURE,   // A Bx    R(A) := closure(KPROTO[Bx])
    VARARG,    // A B     R(A), R(A+1), ..., R(A+B-2) = vararg
    EXTRAARG,  // Ax      extra (larger) argument for previous opcode
    N,         // COUNT
}

internal static class OpCodeExtensions
{
    public static OpModes Modes(this OpCode op)    => LuaOpCodes.OP_CODE_MODES[(int)op];
    public static string  ToString(this OpCode op) => LuaOpCodes.OP_NAMES[(int)op];
}

internal readonly struct OpModes
{
    /*
     * masks for instruction properties. The format is:
     * bits 0-1: op mode
     * bits 2-3: C arg mode
     * bits 4-5: B arg mode
     * bit 6: instruction set register A
     * bit 7: operator is a test (next instruction must be a jump)
     */
    private readonly byte _modes;

    /// <summary>Generate instruction mode (8-byte mask)</summary>
    /// <param name="t">Is conditional statement</param>
    /// <param name="a">Instruction sets register A</param>
    /// <param name="b">B parameter mode</param>
    /// <param name="c">C parameter mode</param>
    /// <param name="m">Operation mode</param>
    internal OpModes(bool t, bool a, OpArgMask b, OpArgMask c, OpType m) {
        int it = t ? 1 : 0;
        int ia = a ? 1 : 0;
        _modes = (byte)((it << 7) | (ia << 6) | ((int)b << 4) | ((int)c << 2) | (int)m);
    }

    public OpType    OpType      => (OpType)(_modes & 3);
    public OpArgMask BMode       => (OpArgMask)((_modes >> 4) & 3);
    public OpArgMask CMode       => (OpArgMask)((_modes >> 2) & 3);
    public bool      SetRegister => (_modes & (1 << 6)) != 0;
    public bool      Conditional => (_modes & (1 << 7)) != 0;
}

/* ArgValue 只支持 RK 值（也就是说有效信息长度 <= 8 位）  */
internal readonly struct ArgValue
{
    public const uint RK_BIT = 1 << (LuaOpCodes.SIZE_B - 1); /* 表示值的类型，1 表示常量表索引，0 表示寄存器 */
    public const byte NO_REG = LuaOpCodes.MAXARG_A;

    private readonly uint _rawValue; /* 最长的参数 Ax 有 26 位 */

    /* 检查只有 sBx 能使用 Constant，其他都要用 RKValue。如果检查没问题可以把 Assert 删除 */
    public bool IsConstantsIndex => (_rawValue & RK_BIT) != 0;
    public bool IsRegister       => (_rawValue & RK_BIT) == 0;
    public bool HasRegister      => (_rawValue & RK_BIT) == 0 && _rawValue != NO_REG;
    public int  RKValue          => (int)(_rawValue & ~RK_BIT);
    public uint Raw              => _rawValue; /* 要创建指令的话直接用 Raw，不要用 RKValue */

    public ArgValue(uint rawArgValue) {
        _rawValue = rawArgValue;
    }

    public static ArgValue FromConstantsIndex(int constantsIndex) {
        LuaDebug.Assert(constantsIndex >= 0 && constantsIndex <= LuaOpCodes.MAX_RK_INDEX, $"Invalid constant index: {constantsIndex}");
        return new ArgValue((uint)constantsIndex | RK_BIT);
    }
    public static ArgValue FromRegister(FrameIndex reg) {
        LuaDebug.Assert(reg >= 0 && reg <= LuaOpCodes.MAXARG_A, $"Invalid register index: {reg}");
        return new ArgValue(reg);
    }

    public ArgValue ChangeRK(bool isConstantsIndex) {
        return new ArgValue(isConstantsIndex ? _rawValue | RK_BIT : _rawValue & ~RK_BIT);
    }

    public override string ToString() {
        if (IsConstantsIndex) {
            return $"k{RKValue}";
        } else if (HasRegister) {
            return $"r{RKValue}";
        } else {
            return "<noreg>";
        }
    }
}

/*
 * 该结构设为 readonly，不想为 struct 提供修改接口，因为容易出错（比如对 List 的索引调用，实际改变的只是拷贝，而不是 List 上的值），如需修改请直接用 Change 接口新建
 * 注意，对于 Instruction 类中所有输入“参数值”的参数，sbx 参数会自动转为移码。其他情况（如：常量索引、偏移量、寄存器索引），
   由调用者负责设置输入的 RK_BIT 标记位。Instruction 方法只会将参数数据原封不动的复制到指令上（会做切割以适配大小）。
 * 也就是说，移码存储是被封装的，外部无感。而 RK_BIT 和 NO_REG 是外部需要处理的
 */
internal readonly struct Instruction
{
    private readonly RawInst _inst;

    private Instruction(RawInst inst) {
        _inst = inst;
    }
    public Instruction(OpCode op, byte a, ushort b, ushort c) { /* 创建 ABC 模式的指令 */
        _inst =
            ((uint)op << LuaOpCodes.POS_OP) | ((uint)a << LuaOpCodes.POS_A) | ((uint)b << LuaOpCodes.POS_B) | ((uint)c << LuaOpCodes.POS_C);
    }
    public Instruction(OpCode op, byte a, int b, bool sbx) { /* 创建 ABx 或 AsBx 模式的指令 */
        if (sbx)
            b += LuaOpCodes.MAXARG_sBx;
        _inst = ((uint)op << LuaOpCodes.POS_OP) | ((uint)a << LuaOpCodes.POS_A) | ((uint)b << LuaOpCodes.POS_Bx);
    }
    public Instruction(OpCode op, uint ax) { /* 创建 Ax 模式的指令 */
        _inst = ((uint)op << LuaOpCodes.POS_OP) | (ax << LuaOpCodes.POS_Ax);
    }

    public OpCode    OpCode   => (OpCode)(_inst >> LuaOpCodes.POS_OP & LuaUtils.Mask1(LuaOpCodes.SIZE_OP, 0));
    public ArgValue  A        => new ArgValue(GetRawArg(_inst, LuaOpCodes.POS_A, LuaOpCodes.SIZE_A));
    public ArgValue  B        => new ArgValue(GetRawArg(_inst, LuaOpCodes.POS_B, LuaOpCodes.SIZE_B));
    public ArgValue  C        => new ArgValue(GetRawArg(_inst, LuaOpCodes.POS_C, LuaOpCodes.SIZE_C));
    public int       Bx       => (int)GetRawArg(_inst, LuaOpCodes.POS_Bx, LuaOpCodes.SIZE_Bx);
    public int       Ax       => (int)GetRawArg(_inst, LuaOpCodes.POS_Ax, LuaOpCodes.SIZE_Ax);
    public int       sBx      => (int)GetRawArg(_inst, LuaOpCodes.POS_Bx, LuaOpCodes.SIZE_Bx) - LuaOpCodes.MAXARG_sBx;
    internal RawInst _RawInst => _inst;

    public OpModes Modes => LuaOpCodes.OP_CODE_MODES[(int)OpCode];

    /* ---------------- Factory ---------------- */

    public Instruction CoplaceOp(OpCode newOpcode) {
        return new Instruction(SetOpCode(_inst, newOpcode));
    }
    public Instruction CoplaceA(byte value) {
        return new Instruction(SetRawArg(_inst, value, LuaOpCodes.POS_A, LuaOpCodes.SIZE_A));
    }
    public Instruction CoplaceB(ushort value) {
        return new Instruction(SetRawArg(_inst, value, LuaOpCodes.POS_B, LuaOpCodes.SIZE_B));
    }
    public Instruction CoplaceC(ushort value) {
        return new Instruction(SetRawArg(_inst, value, LuaOpCodes.POS_C, LuaOpCodes.SIZE_C));
    }
    public Instruction CoplaceBx(uint value) {
        return new Instruction(SetRawArg(_inst, value, LuaOpCodes.POS_Bx, LuaOpCodes.SIZE_Bx));
    }
    public Instruction CoplaceAx(uint value) {
        return new Instruction(SetRawArg(_inst, value, LuaOpCodes.POS_Ax, LuaOpCodes.SIZE_Ax));
    }
    public Instruction CoplaceSBx(int value) {
        return CoplaceBx((uint)(value + LuaOpCodes.MAXARG_sBx));
    }

    internal static Instruction FromOp(OpCode op, ArgValue argA, ArgValue argB, ArgValue argC) {
        switch (op.Modes().OpType) {
        case OpType.ABC:
            return new Instruction(op, (byte)argA.Raw, (ushort)argB.Raw, (ushort)argC.Raw);
        case OpType.ABx:
            return new Instruction(op, (byte)argA.Raw, (int)argB.Raw, false);
        case OpType.AsBx:
            return new Instruction(op, (byte)argA.Raw, (int)argB.Raw, true);
        case OpType.Ax:
            return new Instruction(op, argA.Raw);
        }
        throw new ArgumentException($"Invalid OpCode: {op}");
    }

    /* ---------------- Utils ---------------- */

    private static RawInst SetOpCode(RawInst inst, OpCode op) {
        RawInst newInst   = inst & LuaUtils.Mask0(LuaOpCodes.SIZE_OP, LuaOpCodes.POS_OP); /* clear old opcode */
        uint    newOpCode = ((uint)op << LuaOpCodes.POS_OP) & LuaUtils.Mask1(LuaOpCodes.SIZE_OP, LuaOpCodes.POS_OP);
        return newInst | newOpCode;
    }

    private static uint GetRawArg(RawInst inst, int pos, int size) {
        return (inst >> pos) & LuaUtils.Mask1(size, 0);
    }
    private static uint SetRawArg(RawInst inst, uint rawArgValue, int pos, int size) {
        return (inst & LuaUtils.Mask0(size, pos)) | ((rawArgValue << pos) & LuaUtils.Mask1(size, pos));
    }

    public override string ToString() {
        OpCode  op    = OpCode;
        OpModes modes = op.Modes();
        switch (modes.OpType) {
        case OpType.ABC:
            return $"<abc {op}-{A}-{B}-{C}>";
        case OpType.ABx:
            return $"<abx {op}-{A}-{Bx}>";
        case OpType.AsBx:
            return $"<asbx {op}-{A}-{sBx}>";
        case OpType.Ax:
            return $"<ax {op}-{Ax}>";
        default:
            return "<unknown instruction>";
        }
    }
}

internal static class LuaOpCodes
{
    /* 参数大小 */
    public const int SIZE_OP = 6;
    public const int SIZE_A  = 8;
    public const int SIZE_Ax = SIZE_C + SIZE_B + SIZE_A;
    public const int SIZE_B  = 9;
    public const int SIZE_Bx = SIZE_C + SIZE_B;
    public const int SIZE_C  = 9;

    /* 参数位置 */
    public const int POS_OP = 0;
    public const int POS_A  = POS_OP + SIZE_OP;
    public const int POS_Ax = POS_A;
    public const int POS_B  = POS_C + SIZE_C;
    public const int POS_Bx = POS_C;
    public const int POS_C  = POS_A + SIZE_A;

    /* 各类型参数最大值 */
    public const int MAXARG_A   = (1 << SIZE_A) - 1;
    public const int MAXARG_Ax  = (1 << SIZE_Ax) - 1;
    public const int MAXARG_B   = (1 << SIZE_B) - 1;
    public const int MAXARG_Bx  = (1 << SIZE_Bx) - 1;
    public const int MAXARG_sBx = MAXARG_Bx >> 1; /* 有符号的 Bx */
    public const int MAXARG_C   = (1 << SIZE_C) - 1;

    public const int MAX_RK_INDEX = (int)(ArgValue.RK_BIT - 1);

    public static readonly string[] OP_NAMES = new string[] {
        "MOVE",     "LOADK",    "LOADKX",   "LOADBOOL", "LOADNIL", "GETUPVAL", "GETTABUP", "GETTABLE", "SETTABUP", "SETUPVAL",
        "SETTABLE", "NEWTABLE", "SELF",     "ADD",      "SUB",     "MUL",      "MOD",      "POW",      "DIV",      "IDIV",
        "BAND",     "BOR",      "BXOR",     "SHL",      "SHR",     "UNM",      "BNOT",     "NOT",      "LEN",      "CONCAT",
        "JMP",      "EQ",       "LT",       "LE",       "TEST",    "TESTSET",  "CALL",     "TAILCALL", "RETURN",   "FORLOOP",
        "FORPREP",  "TFORCALL", "TFORLOOP", "SETLIST",  "CLOSURE", "VARARG",   "EXTRAARG", null
    };

    public static readonly OpModes[] OP_CODE_MODES = new OpModes[] {
        /*              T      A     B                 C                 mode              opcode */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.ABC),   /* OP_MOVE */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgN, OpType.ABx),   /* OP_LOADK */
        new OpModes(false, true, OpArgMask.ArgN, OpArgMask.ArgN, OpType.ABx),   /* OP_LOADKX */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgU, OpType.ABC),   /* OP_LOADBOOL */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgN, OpType.ABC),   /* OP_LOADNIL */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgN, OpType.ABC),   /* OP_GETUPVAL */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgK, OpType.ABC),   /* OP_GETTABUP */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgK, OpType.ABC),   /* OP_GETTABLE */
        new OpModes(false, false, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),  /* OP_SETTABUP */
        new OpModes(false, false, OpArgMask.ArgU, OpArgMask.ArgN, OpType.ABC),  /* OP_SETUPVAL */
        new OpModes(false, false, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),  /* OP_SETTABLE */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgU, OpType.ABC),   /* OP_NEWTABLE */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgK, OpType.ABC),   /* OP_SELF */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_ADD */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_SUB */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_MUL */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_MOD */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_POW */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_DIV */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_IDIV */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_BAND */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_BOR */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_BXOR */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_SHL */
        new OpModes(false, true, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_SHR */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.ABC),   /* OP_UNM */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.ABC),   /* OP_BNOT */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.ABC),   /* OP_NOT */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.ABC),   /* OP_LEN */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgR, OpType.ABC),   /* OP_CONCAT */
        new OpModes(false, false, OpArgMask.ArgR, OpArgMask.ArgN, OpType.AsBx), /* OP_JMP */
        new OpModes(true, false, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_EQ */
        new OpModes(true, false, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_LT */
        new OpModes(true, false, OpArgMask.ArgK, OpArgMask.ArgK, OpType.ABC),   /* OP_LE */
        new OpModes(true, false, OpArgMask.ArgN, OpArgMask.ArgU, OpType.ABC),   /* OP_TEST */
        new OpModes(true, true, OpArgMask.ArgR, OpArgMask.ArgU, OpType.ABC),    /* OP_TESTSET */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgU, OpType.ABC),   /* OP_CALL */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgU, OpType.ABC),   /* OP_TAILCALL */
        new OpModes(false, false, OpArgMask.ArgU, OpArgMask.ArgN, OpType.ABC),  /* OP_RETURN */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.AsBx),  /* OP_FORLOOP */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.AsBx),  /* OP_FORPREP */
        new OpModes(false, false, OpArgMask.ArgN, OpArgMask.ArgU, OpType.ABC),  /* OP_TFORCALL */
        new OpModes(false, true, OpArgMask.ArgR, OpArgMask.ArgN, OpType.AsBx),  /* OP_TFORLOOP */
        new OpModes(false, false, OpArgMask.ArgU, OpArgMask.ArgU, OpType.ABC),  /* OP_SETLIST */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgN, OpType.ABx),   /* OP_CLOSURE */
        new OpModes(false, true, OpArgMask.ArgU, OpArgMask.ArgN, OpType.ABC),   /* OP_VARARG */
        new OpModes(false, false, OpArgMask.ArgU, OpArgMask.ArgU, OpType.Ax)    /* OP_EXTRAARG */
    };
}

}
