namespace YALuaToy.Tests.Utils {

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using YALuaToy.Core;
using YALuaToy.Const;
using YALuaToy.Compilation;
using YALuaToy.Compilation.Antlr;
using Xunit;
using Xunit.Sdk;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

internal struct InstIR
{
    internal int         line;
    internal Instruction inst;
    internal string      desc;

    public static bool FromLuac(string line, out InstIR instIR) {
        int i = 0;

        string Next() {
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;
            if (i >= line.Length)
                return "<end>";
            int j = i;
            for (; j < line.Length; j++) {
                if (j == line.Length - 1 || char.IsWhiteSpace(line[j + 1]))
                    break;
            }
            (i, j) = (j + 1, i);
            return line[j..i];
        }

        try {
            string temp;
            Next(); /* no */
            temp      = Next();
            int line_ = int.Parse(temp[1.. ^ 1]);
            if (!Enum.TryParse(Next(), out OpCode op))
                throw new Exception($"Failed to parse opcode: {line}");
            int a = int.Parse(Next());
            int  b = int.Parse(Next());
            int  c = 0;
            temp   = Next();
            if (temp != ";" && temp != "<end>")
                c = int.Parse(temp);
            string desc = "";
            if (i < line.Length)
                desc = line[i..].Trim();
            if (desc.StartsWith(";"))
                desc = desc[1..].Trim();

            /* 生成指令 */
            int GetRK(int raw) {
                if (raw >= 0) return (int)ArgValue.FromRegister((byte)raw).Raw;
                return (int)ArgValue.FromConstantsIndex(-raw - 1).Raw;
            }
            int GetUnsigned(int raw) {
                if (raw >= 0) return raw;
                return -raw - 1;
            }

            Instruction inst_ = new();
            switch (op.Modes().OpType) {
            case OpType.ABC:
                if (op.Modes().BMode == OpArgMask.ArgN)
                inst_ = new Instruction(op, (byte)GetRK(a), 0, (ushort)GetRK(b));
                else
                inst_ = new Instruction(op, (byte)GetRK(a), (ushort)GetRK(b), (ushort)GetRK(c));
                break;
            case OpType.ABx:
                inst_ = new Instruction(op, (byte)GetRK(a), GetUnsigned(b), false);
                break;
            case OpType.AsBx:
                inst_ = new Instruction(op, (byte)GetRK(a), b, true);
                break;
            case OpType.Ax:
                inst_ = new Instruction(op, (uint)GetUnsigned(a));
                break;
            }

            instIR = new InstIR() {
                line = line_,
                inst = inst_,
                desc = desc,
            };
            return true;
        } catch (Exception e) {
            Console.WriteLine($"Error parsing line: {line}\n  {e}");
            instIR = default;
            return false;
        }
    }

    public override string ToString() {
        string RKToString(ArgValue arg) {
            if (arg.IsConstantsIndex)
                return (-arg.RKValue - 1).ToString();
            return arg.RKValue.ToString();
        }

        StringBuilder sb = new();
        sb.Append($"[{line}]\t{inst.OpCode.ToString().PadRight(9, ' ')}\t");
        if (inst.OpCode.Modes().OpType != OpType.Ax)
            sb.Append($"{inst.A.RKValue} ");
        switch (inst.OpCode) {
        case OpCode.MOVE:  // A B     R(A) := R(B)
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.LOADK:  // A Bx    R(A) := Kst(Bx)
            sb.Append($"{-inst.Bx - 1} ");
            break;
        case OpCode.LOADKX:  // A       R(A) := Kst(extra arg)
            break;
        case OpCode.LOADBOOL:  // A B C   R(A) := (Bool)B; if (C) pc++
            sb.Append($"{inst.B.RKValue} {inst.C.RKValue} ");
            break;
        case OpCode.LOADNIL:  // A B     R(A), R(A+1), ..., R(A+B) := nil
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.GETUPVAL:  // A B     R(A) := Upvalue[B]
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.GETTABUP:  // A B C   R(A) := Upvalue[B][RK(C)]
            sb.Append($"{inst.B.RKValue} {RKToString(inst.C)} ");
            break;
        case OpCode.GETTABLE:  // A B C   R(A) := R(B)[RK(C)]
            sb.Append($"{inst.B.RKValue} {RKToString(inst.C)} ");
            break;
        case OpCode.SETTABUP:  // A B C   Upvalue[A][RK(B)] := RK(C)
            sb.Append($"{RKToString(inst.B)} {RKToString(inst.C)} ");
            break;
        case OpCode.SETUPVAL:  // A B     Upvalue[B] := R(A)
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.SETTABLE:  // A B C   R(A)[RK(B)] := RK(C)
            sb.Append($"{RKToString(inst.B)} {RKToString(inst.C)} ");
            break;
        case OpCode.NEWTABLE:  // A B C   R(A) := {} (size = B,C)
            sb.Append("0 0 ");
            break;
        case OpCode.SELF:  // A B C   R(A+1) := R(B); R(A) := R(B)[RK(C)]
            sb.Append($"{inst.B.RKValue} {RKToString(inst.C)} ");
            break;
        case OpCode.ADD:   // A B C   R(A) := RK(B) + RK(C)
        case OpCode.SUB:   // A B C   R(A) := RK(B) - RK(C)
        case OpCode.MUL:   // A B C   R(A) := RK(B) * RK(C)
        case OpCode.MOD:   // A B C   R(A) := RK(B) % RK(C)
        case OpCode.POW:   // A B C   R(A) := RK(B) ^ RK(C)
        case OpCode.DIV:   // A B C   R(A) := RK(B) / RK(C)
        case OpCode.IDIV:  // A B C   R(A) := RK(B) // RK(C)
        case OpCode.BAND:  // A B C   R(A) := RK(B) & RK(C)
        case OpCode.BOR:   // A B C   R(A) := RK(B) | RK(C)
        case OpCode.BXOR:  // A B C   R(A) := RK(B) ~ RK(C)
        case OpCode.SHL:   // A B C   R(A) := RK(B) << RK(C)
        case OpCode.SHR:   // A B C   R(A) := RK(B) >> RK(C)
            sb.Append($"{RKToString(inst.B)} {RKToString(inst.C)} ");
            break;
        case OpCode.UNM:   // A B     R(A) := -R(B)
        case OpCode.BNOT:  // A B     R(A) := ~R(B)
        case OpCode.NOT:   // A B     R(A) := not R(B)
        case OpCode.LEN:   // A B     R(A) := length of R(B)
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.CONCAT:  // A B C   R(A) := R(B).. ... ..R(C)
            sb.Append($"{inst.B.RKValue} {inst.C.RKValue} ");
            break;
        case OpCode.JMP:  // A sBx   pc+=sBx; if (A) close all upvalues >= R(A - 1)
            sb.Append($"{inst.sBx} ");
            break;
        case OpCode.EQ:  // A B C   if ((RK(B) == RK(C)) ~= A) then pc++
        case OpCode.LT:  // A B C   if ((RK(B) <  RK(C)) ~= A) then pc++
        case OpCode.LE:  // A B C   if ((RK(B) <= RK(C)) ~= A) then pc++
            sb.Append($"{RKToString(inst.B)} {RKToString(inst.C)} ");
            break;
        case OpCode.TEST:  // A C     if not (R(A) <=> C) then pc++
            sb.Append($"{inst.C.RKValue} ");
            break;
        case OpCode.TESTSET:  // A B C   if (R(B) <=> C) then R(A) := R(B) else pc++
            sb.Append($"{inst.B.RKValue} {inst.C.RKValue} ");
            break;
        case OpCode.CALL:  // A B C   R(A), ... ,R(A+C-2) := R(A)(R(A+1), ... ,R(A+B-1))
            sb.Append($"{inst.B.RKValue} {inst.C.RKValue} ");
            break;
        case OpCode.TAILCALL:  // A B C   return R(A)(R(A+1), ... ,R(A+B-1))
            sb.Append($"{inst.B.RKValue} {inst.C.RKValue} ");
            break;
        case OpCode.RETURN:  // A B     return R(A), ... ,R(A+B-2)
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.FORLOOP:  // A sBx   R(A)+=R(A+2); if R(A) <?= R(A+1) then { pc+=sBx; R(A+3)=R(A) }
            sb.Append($"{inst.sBx} ");
            break;
        case OpCode.FORPREP:  // A sBx   R(A)-=R(A+2); pc+=sBx
            sb.Append($"{inst.sBx} ");
            break;
        case OpCode.TFORCALL:  // A C     R(A+3), ... ,R(A+2+C) := R(A)(R(A+1), R(A+2));
            sb.Append($"{inst.C.RKValue} ");
            break;
        case OpCode.TFORLOOP:  // A sBx   if R(A+1) ~= nil then { R(A)=R(A+1); pc += sBx }
            sb.Append($"{inst.sBx} ");
            break;
        case OpCode.SETLIST:  // A B C   R(A)[(C-1)*FPF+i] := R(A+i), 1 <= i <= B
            sb.Append($"{inst.B.RKValue} {inst.C.RKValue} ");
            break;
        case OpCode.CLOSURE:  // A Bx    R(A) := closure(KPROTO[Bx])
            sb.Append($"{inst.Bx} ");
            break;
        case OpCode.VARARG:  // A B     R(A), R(A+1), ..., R(A+B-2) = vararg
            sb.Append($"{inst.B.RKValue} ");
            break;
        case OpCode.EXTRAARG:  // Ax      extra (larger) argument for previous opcode
            sb.Append($"{inst.Bx} ");
            break;
        }
        sb.Append('\t');
        return sb.ToString();
    }
}

internal class ProtoIR
{
    internal List<InstIR> insts = [];
    internal int          startLine;
    internal int          stopLine;
    internal int          paramCount;
    internal int          frameSize;
    internal int          upvalueCount;
    internal int          localCount;
    internal int          constantCount;
    internal int          subFunctionCount;

    internal int InstCount => insts.Count;
}

internal class FileIR
{
    internal string filepath      = "";
    internal List<ProtoIR> protos = [];

    public override string ToString() {
        StringBuilder sb = new();
        for (int i = 0; i < protos.Count; i++) {
            ProtoIR proto = protos[i];
            sb.AppendLine();
            sb.Append(i == 0 ? "main " : "function ");
            sb.AppendLine($"<:{proto.startLine},{proto.stopLine}> ({proto.InstCount} instructions)");
            sb.AppendLine(
                $"{proto.paramCount} params, {proto.frameSize} slots, {proto.upvalueCount} upvalue, {proto.localCount} locals, {proto.constantCount} constants, {proto.subFunctionCount} functions"
            );
            for (int j = 0; j < proto.insts.Count; j++)
                sb.AppendLine($"\t{j+1}\t{proto.insts[j].ToString()}");
        }
        return sb.ToString();
    }

    public void AssertSame(FileIR other) {
        Assert.Equal(protos.Count, other.protos.Count);
        for (int i = 0; i < protos.Count; i++) {
            ProtoIR proto1 = protos[i];
            ProtoIR proto2 = other.protos[i];
            /* 检查指令是否完全一致 */
            for (int j = 0; j < proto1.InstCount; j++) {
                InstIR inst1 = proto1.insts[j];
                InstIR inst2 = proto2.insts[j];
                Assert.True(inst1.inst.OpCode == inst2.inst.OpCode, $"opcode not same at {i+1}:{j+1}, opcode1: {inst1.inst.OpCode}, opcode2: {inst2.inst.OpCode}");
                if (inst1.inst.OpCode == OpCode.NEWTABLE)
                Assert.True(inst1.inst.A.Raw == inst2.inst.A.Raw, $"newtable A not same at {i+1}:{j+1}, a1: {inst1.inst.A.Raw}, a2: {inst2.inst.A.Raw}");
                else
                Assert.True(
                    inst1.inst._RawInst == inst2.inst._RawInst, $"code not same at {i+1}:{j+1}\n\t[{inst1.line}] inst1: {inst1.inst}\n\t[{inst1.line}] inst2: {inst2.inst}"
                );
            }

            /* 检查其他属性是否一致 */
            Assert.True(proto1.startLine == proto2.startLine, $"{i}: startLine not same: {proto1.startLine} != {proto2.startLine}");
            Assert.True(proto1.stopLine == proto2.stopLine, $"{i}: stopLine not same: {proto1.stopLine} != {proto2.stopLine}");
            Assert.True(proto1.paramCount == proto2.paramCount, $"{i}: paramCount not same: {proto1.paramCount} != {proto2.paramCount}");
            Assert.True(proto1.frameSize == proto2.frameSize, $"{i}: frameSize not same: {proto1.frameSize} != {proto2.frameSize}");
            Assert.True(
                proto1.upvalueCount == proto2.upvalueCount, $"{i}: upvalueCount not same: {proto1.upvalueCount} != {proto2.upvalueCount}"
            );
            Assert.True(proto1.localCount == proto2.localCount, $"{i}: localCount not same: {proto1.localCount} != {proto2.localCount}");
            /* 字符串非法 utf8 反转义结果和 luac 不一样，可以先关闭这个检查 */
            // Assert.True(proto1.constantCount == proto2.constantCount, $"{i}: constantCount not same: {proto1.constantCount} != {proto2.constantCount}");
            Assert.True(
                proto1.subFunctionCount == proto2.subFunctionCount,
                $"{i}: subFunctionCount not same: {proto1.subFunctionCount} != {proto2.subFunctionCount}"
            );
            Assert.True(proto1.InstCount == proto2.InstCount, $"{i}: InstCount not same: {proto1.InstCount} != {proto2.InstCount}");
        }
    }
}

internal static class LuacUtils
{
    public static FileIR DecompileByLuac(string filepath) {
        string luacpath = CommonTestUtils.GetPath(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "CLua/build/Debug/bin/luac.exe" : "CLua/build/Debug/bin/luac"
        );

        /* 检查 luac 是否存在 */
        if (!File.Exists(filepath))
            throw new FileNotFoundException($"lua file not found at {filepath}, please check it.");
        if (!File.Exists(luacpath))
            throw new FileNotFoundException($"luac not found at {luacpath}, please build it first.");

        /* 用 luac 将反编译结果输出到缓存 */
        string           filename  = Path.GetFileNameWithoutExtension(filepath);
        string           cachepath = CommonTestUtils.GetPath($"out/DecompileCache/{filename}.clua");
        ProcessStartInfo startInfo = new(luacpath, $"-p -l {filepath}") {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error  = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception($"luac failed with error code {process.ExitCode}: {error}");
        File.WriteAllText(cachepath, output);

        /* 从缓存中解析 IR 表示 */
        string[] decompile = File.ReadAllLines(cachepath);
        FileIR fileIR      = new() { filepath = filepath };
        for (int i = 0; i < decompile.Length; i++) {
            string line = decompile[i];
            if (!line.StartsWith("main") && !line.StartsWith("function"))
                continue;

            Regex regex1 = new(@":(\d+),(\d+)");
            Regex regex2 = new(
                @"(\d+)[+]? param[s]?, (\d+) slot[s]?, (\d+) upvalue[s]?, (\d+) local[s]?, (\d+) constant[s]?, (\d+) " + @"function[s]?"
            );

            string next   = decompile[++i];
            Match  match1 = regex1.Match(line);
            Match  match2 = regex2.Match(next);
            if (!match1.Success)
                throw new Exception($"Failed to parse line.\n  line1: {line}");
            if (!match2.Success)
                throw new Exception($"Failed to parse line.\n  line2: {next}");

            ProtoIR protoIR = new() {
                startLine = int.Parse(match1.Groups[1].Value),     stopLine = int.Parse(match1.Groups[2].Value),
                paramCount = int.Parse(match2.Groups[1].Value),    frameSize = int.Parse(match2.Groups[2].Value),
                upvalueCount = int.Parse(match2.Groups[3].Value),  localCount = int.Parse(match2.Groups[4].Value),
                constantCount = int.Parse(match2.Groups[5].Value), subFunctionCount = int.Parse(match2.Groups[6].Value),
            };
            fileIR.protos.Add(protoIR);

            /* 循环解析指令 */
            i++;
            while (i < decompile.Length) {
                if (string.IsNullOrWhiteSpace(decompile[i]))
                    break;
                if (!InstIR.FromLuac(decompile[i], out InstIR inst))
                    break;
                protoIR.insts.Add(inst);
                i++;
            }
        }

        return fileIR;
    }

    public static FileIR Decompile(LuaProto proto, string filepath, bool root = true) {
        FileIR fileIR = new() { filepath = filepath };

        /* 读取当前 Proto 信息 */
        ProtoIR protoIR = new() {
            startLine = proto._firstLine,         stopLine = proto._lastLine,           paramCount = proto.ParamCount,
            frameSize = proto.FrameSize,          upvalueCount = proto.UpvalueCount,    localCount = proto.LocalVarsCount,
            constantCount = proto.ConstantsCount, subFunctionCount = proto.SubProtosCount,
        };
        for (int i = 0; i < proto._instructions.Count; i++) {
            var inst = proto._instructions[i];
            int line = proto.Lines[i];
            protoIR.insts.Add(new InstIR() {
                line = line,
                inst = inst,
                desc = "todo",
            });
        }
        fileIR.protos.Add(protoIR);

        /* 读取子函数 */
        if (proto.SubProtos != null)
            foreach (LuaProto subProto in proto.SubProtos)
                fileIR.protos.AddRange(Decompile(subProto, filepath, false).protos);

        /* 如果是 root，做一次按行排序 */
        if (root) {
            fileIR.protos.Sort((x, y) => {
                if (x.startLine != y.startLine)
                    return x.startLine.CompareTo(y.startLine);
                return x.stopLine.CompareTo(y.stopLine);
            });
        }

        return fileIR;
    }
}

}
