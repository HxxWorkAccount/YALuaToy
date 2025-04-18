namespace YALuaToy.Core {

using System;
using System.Collections.Generic;
using YALuaToy.Const;
using YALuaToy.Debug;

public partial class LuaState
{
    internal void Execute() {
        _calledMark              = true;
        CallInfo       vmCI      = _currCI;
        CallInfo       _prevVMCI = _vmCI;
        LClosure       currClosure;
        List<LuaValue> constants;
        RawIdx         firstArg;
        vmCI.SetCallStatusFlag(CallStatus.FRESH, true);

        /* 通用获取，R 开头表示获取寄存器，RK 则是获取；注意这里不会做安全检查 */
        /* 另外，这里的 R 和 RK 表现和 CLua 的也不同。R 不是返回引用而是返回栈地址；而 RK 则直接返回值（这意味着涉及写入的场景要另想办法） */
        RawIdx RA(Instruction inst) {
            return firstArg + inst.A.RKValue;
        }
        RawIdx RB(Instruction inst) {
            LuaDebug.Check(inst.Modes.BMode == OpArgMask.ArgR, $"Unexpected BMode: {inst.Modes.BMode}");
            return firstArg + inst.B.RKValue;
        }
        RawIdx RC(Instruction inst) {
            LuaDebug.Check(inst.Modes.CMode == OpArgMask.ArgR, $"Unexpected CMode: {inst.Modes.CMode}");
            return firstArg + inst.C.RKValue;
        }
        LuaValue RKB(Instruction inst) {
            LuaDebug.Check(inst.Modes.BMode == OpArgMask.ArgK, $"Unexpected BMode: {inst.Modes.BMode}");
            ArgValue b = inst.B;
            return b.IsConstantsIndex ? constants[b.RKValue] : GetStack(firstArg + b.RKValue);
        }
        LuaValue RKC(Instruction inst) {
            LuaDebug.Check(inst.Modes.CMode == OpArgMask.ArgK, $"Unexpected CMode: {inst.Modes.CMode}");
            ArgValue c = inst.C;
            return c.IsConstantsIndex ? constants[c.RKValue] : GetStack(firstArg + c.RKValue);
        }

        int  fetchCounter = 0; /* 调试用途 */
        void Fetch(out Instruction inst, out RawIdx ra) {
            inst = vmCI.GetInstruction(vmCI.PC++);
            ra   = RA(inst);
            LuaDebug.Assert(firstArg == vmCI.FirstArg);
            LuaDebug.Assert(firstArg <= _top && _top < (RawIdx)0 + _StackSize);
            fetchCounter++;
            _vmCI = vmCI;
#if DEBUG
            globalState.Logger?.LogLine(
                "VM",
                $"[{fetchCounter}] line {currClosure.proto.Lines[vmCI.PC-1]}, func: {vmCI.Func}~{_top} at line {currClosure.proto._firstLine}, pc: {vmCI.PC-1}, ccalls: {cCalls} | {inst}"
            );
            // globalState.Logger?.WriteLine(
            //     $"[{fetchCounter}] line {currClosure.proto.Lines[vmCI.PC-1]}, func: {vmCI.Func}~{_top} at line {currClosure.proto._firstLine}, pc: {vmCI.PC-1}, ccalls: {cCalls} | {inst}"
            // );
#endif
        }

    NewFrame:
        _vmCI = vmCI;
        LuaDebug.Assert(vmCI == _currCI);
        currClosure = _stack[(int)vmCI.Func].LObject<LClosure>();
        LuaDebug.Assert(currClosure == _currCI.LClosure);
        firstArg  = vmCI.FirstArg;
        constants = currClosure.proto.Constants;
        while (true) {
            Fetch(out Instruction inst, out RawIdx ra);
            switch (inst.OpCode) {
            case OpCode.MOVE: { /* 赋值 | R(A) := R(B) */
                RawIdx rb       = RB(inst);
                _stack[(int)ra] = _stack[(int)rb];
                break;
            }
            case OpCode.LOADK: /* 读取常量（常量表偏移） | R(A) := Kst(Bx) */
                _stack[(int)ra] = constants[inst.Bx];
                break;
            case OpCode.LOADKX: { /* 从【拓展参数】中读取常量，下条指令一定是 extraarg | R(A) := Kst(extra arg) */
                Instruction nextInst = vmCI.NextInst;
                LuaDebug.AssertInstExtraarg(nextInst);
                _stack[(int)ra] = constants[nextInst.Ax];
                vmCI.PC++;
                break;
            }
            case OpCode.LOADBOOL: /* 多功能复合指令，读取参数 B 上的布尔值，并根据参数 C 决定是否跳转 | R(A) := (Bool)B; if (C) pc++ */
                _stack[(int)ra] = inst.B.RKValue == 0 ? LuaValue.FALSE : LuaValue.TRUE;
                if (inst.C.RKValue != 0)
                    vmCI.PC++;
                break;
            case OpCode.LOADNIL: /* 批量设置 nil；R(A), R(A+1), ..., R(A+B) := nil */
                /* 注意该指令会增加 ra */
                for (int b = inst.B.RKValue; b >= 0; b--, ra++)
                    _stack[(int)ra] = LuaValue.NIL;
                break;
            case OpCode.GETUPVAL: /* 读取上值 | R(A) := Upvalue[B] */
                _stack[(int)ra] = currClosure.GetUpvalue(inst.B.RKValue + 1);
                break;
            case OpCode.GETTABUP: { /* 上值表索引 | R(A) := Upvalue[B][RK(C)] */
                LuaValue table  = currClosure.GetUpvalue(inst.B.RKValue + 1);
                LuaValue key    = RKC(inst);
                _stack[(int)ra] = Index(table, key);
                break;
            }
            case OpCode.GETTABLE: { /* 栈上表索引 | R(A) := R(B)[RK(C)] */
                LuaValue table  = _stack[(int)RB(inst)];
                LuaValue key    = RKC(inst);
                _stack[(int)ra] = Index(table, key);
                break;
            }
            case OpCode.SETTABUP: { /* 上值表索引赋值 | Upvalue[A][RK(B)] := RK(C) */
                LuaValue table = currClosure.GetUpvalue(inst.A.RKValue + 1);
                LuaValue key   = RKB(inst);
                LuaValue value = RKC(inst);
                NewIndex(table, key, value);
                break;
            }
            case OpCode.SETUPVAL: { /* 设置上值 | Upvalue[B] := R(A) */
                Upvalue upvalue = currClosure.GetUpvalueObj(inst.B.RKValue + 1);
                if (upvalue.Open) /* 如果打开，就直接改栈上的值 */
                    upvalue.Thread._stack[(int)upvalue.RawIdx] = _stack[(int)ra];
                else
                    currClosure.SetUpvalue(inst.B.RKValue + 1, _stack[(int)ra]);
                break;
            }
            case OpCode.SETTABLE: { /* 栈上表索引赋值 | R(A)[RK(B)] := RK(C) */
                LuaValue table = _stack[(int)ra];
                LuaValue key   = RKB(inst);
                LuaValue value = RKC(inst);
                NewIndex(table, key, value);
                break;
            }
            case OpCode.NEWTABLE: { /* 新建表，目前不支持预指定容量 | R(A) := {} */
                _stack[(int)ra] = new LuaValue(new LuaTable());
                break;
            }
            case OpCode.SELF: { /* 自赋值 | R(A+1) := R(B); R(A) := R(B)[RK(C)] */
                RawIdx   rb    = RB(inst);
                LuaValue table = _stack[(int)rb];
                LuaValue key   = RKC(inst);
                LuaDebug.AssertTag(key, LuaConst.TSTRING);
                _stack[(int)(ra + 1)] = _stack[(int)rb];
                _stack[(int)ra]       = Index(table, key);
                break;
            }
            case OpCode.ADD: /* R(A) := RK(B) + RK(C)  */
                _stack[(int)ra] = Arith(Op.ADD, RKB(inst), RKC(inst));
                break;
            case OpCode.SUB: /* R(A) := RK(B) - RK(C) */
                _stack[(int)ra] = Arith(Op.SUB, RKB(inst), RKC(inst));
                break;
            case OpCode.MUL: /* R(A) := RK(B) * RK(C) */
                _stack[(int)ra] = Arith(Op.MUL, RKB(inst), RKC(inst));
                break;
            case OpCode.DIV: /* 浮点数除法，float division (always with floats) | R(A) := RK(B) / RK(C) */
                _stack[(int)ra] = Arith(Op.DIV, RKB(inst), RKC(inst));
                break;
            case OpCode.BAND: /* 按位与 | R(A) := RK(B) & RK(C) */
                _stack[(int)ra] = Arith(Op.BAND, RKB(inst), RKC(inst));
                break;
            case OpCode.BOR: /* 按位或 | R(A) := RK(B) | RK(C) */
                _stack[(int)ra] = Arith(Op.BOR, RKB(inst), RKC(inst));
                break;
            case OpCode.BXOR: /* 按位异或 | R(A) := RK(B) ^ RK(C) */
                _stack[(int)ra] = Arith(Op.BXOR, RKB(inst), RKC(inst));
                break;
            case OpCode.SHL: /* 逻辑左移 | R(A) := RK(B) << RK(C) */
                _stack[(int)ra] = Arith(Op.SHL, RKB(inst), RKC(inst));
                break;
            case OpCode.SHR: /* 逻辑右移 | R(A) := RK(B) >> RK(C) */
                _stack[(int)ra] = Arith(Op.SHR, RKB(inst), RKC(inst));
                break;
            case OpCode.MOD: /* 取模 | R(A) := RK(B) % RK(C) */
                _stack[(int)ra] = Arith(Op.MOD, RKB(inst), RKC(inst));
                break;
            case OpCode.IDIV: /* 整型 floor 除（向下取整，向负无穷取整） | R(A) := RK(B) // RK(C) */
                _stack[(int)ra] = Arith(Op.IDIV, RKB(inst), RKC(inst));
                break;
            case OpCode.POW: /* 指数运算 | R(A) := RK(B) ^ RK(C) */
                _stack[(int)ra] = Arith(Op.POW, RKB(inst), RKC(inst));
                break;
            case OpCode.UNM: /* 取负数 | R(A) := -R(B) */
                _stack[(int)ra] = Arith(Op.UNM, _stack[(int)RB(inst)], LuaValue.ZERO);
                break;
            case OpCode.BNOT: /* 按位取反 | R(A) := ~R(B) */
                _stack[(int)ra] = Arith(Op.BNOT, _stack[(int)RB(inst)], LuaValue.ZERO);
                break;
            case OpCode.NOT: { /* 逻辑否 | R(A) := not R(B) */
                LuaValue b      = _stack[(int)RB(inst)];
                _stack[(int)ra] = !b.ToBoolean() ? LuaValue.TRUE : LuaValue.FALSE;
                break;
            }
            case OpCode.LEN: /* 取长度 | R(A) := length of R(B) */
                GetLength(_stack[(int)RB(inst)], ra);
                break;
            case OpCode.CONCAT: { /* 连接运算 | R(A) := R(B).. ... ..R(C) */
                int start = inst.B.RKValue;
                int end   = inst.C.RKValue;
                _top      = firstArg + end + 1; /* 暂时把 _top “降低”到 C 的下个位置，这样 Concat 函数才能正常工作 */
                Concat(end - start + 1);
                _stack[(int)ra] = _stack[(int)RB(inst)];
                _top            = vmCI.Top;
                break;
            }
            case OpCode.JMP: /* 无条件跳转，并关闭需要关闭的上值 | pc+=sBx; if (A) close all upvalues >= R(A - 1) */
                _DoJump(vmCI, inst, 0);
                break;
            case OpCode.EQ: { /* 条件分支 == | if ((RK(B) == RK(C)) ~= A) then pc++ */
                if (Equals(RKB(inst), RKC(inst)) != (inst.A.RKValue != 0))
                    vmCI.PC++;
                else
                    _DoNextJump(vmCI);
                break;
            }
            case OpCode.LT: { /* 条件分支 < | if ((RK(B) <  RK(C)) ~= A) then pc++ */
                if (LessThan(RKB(inst), RKC(inst)) != (inst.A.RKValue != 0))
                    vmCI.PC++;
                else
                    _DoNextJump(vmCI);
                break;
            }
            case OpCode.LE: { /* 条件分支 <= | if ((RK(B) <= RK(C)) ~= A) then pc++ */
                if (LessEqual(RKB(inst), RKC(inst)) != (inst.A.RKValue != 0))
                    vmCI.PC++;
                else
                    _DoNextJump(vmCI);
                break;
            }
            case OpCode.TEST: { /* 条件分支，从寄存器取值 | if not (R(A) <=> C) then pc++ */
                LuaValue value = _stack[(int)ra];
                if (value.ToBoolean() != (inst.C.RKValue != 0))
                    vmCI.PC++;
                else
                    _DoNextJump(vmCI);
                break;
            }
            case OpCode.TESTSET: { /* 条件赋值 | if (R(B) <=> C) then R(A) := R(B) else pc++ */
                LuaValue bvalue = _stack[(int)RB(inst)];
                if (bvalue.ToBoolean() == (inst.C.RKValue != 0)) {
                    _stack[(int)ra] = bvalue;
                    _DoNextJump(vmCI);
                } else {
                    vmCI.PC++;
                }
                break;
            }
            case OpCode.CALL: { /* 函数调用，支持多参数和多返回值 | R(A), ... ,R(A+C-2) := R(A)(R(A+1), ... ,R(A+B-1)) */
                int argCount    = inst.B.RKValue - 1;
                int resultCount = inst.C.RKValue - 1;
                if (argCount >= 0)
                    _top = ra + argCount + 1;
                if (_PreCall(ra, (short)resultCount)) { /* C 函数 */
                    /* 关于这里，我猜是因为编译期无法知道多返回值的数量，所以直接不改变多返回值时的 _top 了（建议用调试观察一下行为） */
                    if (resultCount >= 0) /* C 函数执行完后回到 Lua 函数，按 Lua 函数规范，_top 始终等于 currCI.Top，这里要重新设一下 */
                        _top = vmCI.Top;
                } else {            /* Lua 函数 */
                    vmCI = _currCI; /* PreCall 中创建了新栈帧，切换一下 ci 直接重新执行即可 */
                    goto NewFrame;
                }
                break;
            }
            case OpCode.TAILCALL: { /* 尾调用 | return R(A)(R(A+1), ... ,R(A+B-1)) */
                int argCount = inst.B.RKValue - 1;
                if (argCount >= 0)
                    _top = ra + argCount + 1;
                LuaDebug.Assert(inst.C.RKValue - 1 == LuaConst.MULTRET, "Tail call must return all results");

                /* C 函数的尾调用没有优化。因为一定多返回，所以不需要做任何事情。而对于 Lua 调用，这里要原地重新初始化 CallInfo 和栈结构，然后重新执行当前栈帧 */
                if (_PreCall(ra, LuaConst.MULTRET)) { /* 这里会新建 CI */
                } else {
                    CallInfo tailcallCI = _currCI;                                               /* _currCI 被 PreCall 更新了 */
                    CallInfo originCI   = tailcallCI.prev;                                       /* 注意 currClosure 是属于 originCI 的 */
                    RawIdx lastArg = tailcallCI.FirstArg + tailcallCI.LClosure.proto.ParamCount; /* PreCall 调整栈后，栈帧最后一个有效元素 */

                    if (currClosure.proto.SubProtosCount > 0) /* 当前调用存在闭包，要关闭上值 */
                        CloseUpvalues(originCI.FirstArg);

                    /* 把 tailcallCI 的栈结构覆盖到 currCI 上 */
                    RawIdx tailcallFunc = tailcallCI.Func;
                    RawIdx originFunc   = originCI.Func;
                    for (int i = 0; tailcallFunc + i < lastArg; i++)
                        _stack[(int)originFunc + i] = _stack[(int)tailcallFunc + i];

                    /* 把 tailcallCI 配置应用到 originCI 上 */
                    originCI.Reset(LuaConst.MULTRET, originFunc, originFunc + (_top - tailcallFunc), originCI.CallStatus);
                    originCI.ResetLuaInfo(originFunc + (tailcallCI.FirstArg - tailcallFunc), originFunc, tailcallCI.PC);
                    originCI.SetCallStatusFlag(CallStatus.TAIL_CALL, true);

                    /* originCI 被 tailcallCI 夺舍完毕，重新执行 originCI，完成尾调用 */
                    _top    = originCI.Top;
                    _currCI = vmCI = originCI;
                    LuaDebug.Assert(_top == originCI.FirstArg + originCI.LClosure.proto.FrameSize, "Tail call must fill the stack");
                    goto NewFrame;
                }
                break;
            }
            case OpCode.RETURN: { /* Lua 函数返回，这里的 B 是 callee 实际返回数量 | return R(A), ... ,R(A+B-2) */
                int resultCount     = inst.B.RKValue - 1;
                int realResultCount = resultCount == LuaConst.MULTRET ? _top - ra : resultCount;

                if (currClosure.proto.SubProtosCount > 0) /* 当前调用存在闭包 */
                    CloseUpvalues(firstArg);
                bool fixedResult = _PosCall(vmCI, ra, realResultCount);

                if (vmCI.GetCallStatusFlag(CallStatus.FRESH)) {
                    /* 这表明此轮 NewFrame 的执行是直接从 Execute 启动的，而不是 NewFrame 递归启动的。如果 Execute 执行的函数已经结束，那就没有后续了 */
                    _vmCI = _prevVMCI;
                    return;
                } else {
                    vmCI = _currCI; /* PosCall 里更新了 _currCI，所以也要更新一下上下文的 currCI */
                    if (fixedResult)
                        _top = vmCI.Top;
                    LuaDebug.Assert(vmCI.IsLua);
                    LuaDebug.Assert(vmCI.GetInstruction(vmCI.PC - 1).OpCode == OpCode.CALL);
                    goto NewFrame;
                }
            }
            case OpCode.FORLOOP: { /* for 循环；R(A)+=R(A+2); if R(A) <?= R(A+1) then { pc+=sBx; R(A+3)=R(A) } */
                LuaValue indexVal = _stack[(int)ra];
                LuaValue limitVal = _stack[(int)ra + 1];
                LuaValue stepVal  = _stack[(int)ra + 2];
                if (indexVal.IsInt) { /* 循环变量是整型 */
                    long step  = stepVal.Int;
                    long index = indexVal.Int + step;
                    long limit = limitVal.Int;
                    if (step > 0 ? index <= limit : index >= limit) { /* 是否在循环范围内 */
                        vmCI.PC += inst.sBx; /* 跳转到循环体（若不执行，就执行 FORLOOP 下一条指令，是一个跳转到函数体后的指令） */
                        _stack[(int)ra]     = new LuaValue(index); /* 更新内部循环变量 */
                        _stack[(int)ra + 3] = new LuaValue(index); /* 更新外部循环变量 */
                    }
                } else { /* 循环变量是浮点数 */
                    double step  = stepVal.Float;
                    double index = indexVal.Float + step;
                    double limit = limitVal.Float;
                    if (step > 0 ? index <= limit : index >= limit) { /* 是否超出循环范围 */
                        vmCI.PC += inst.sBx;                          /* 跳转到循环体 */
                        _stack[(int)ra]     = new LuaValue(index);    /* 更新内部循环变量 */
                        _stack[(int)ra + 3] = new LuaValue(index);    /* 更新外部循环变量 */
                    }
                }
                break;
            }
            case OpCode.FORPREP: { /* for-prepare，准备（初始化）循环 | R(A)-=R(A+2); pc+=sBx */
                /* 这里要执行 R(A)-=R(A+2)，因为 FORLOOP 里会无脑先更新循环变量，再判断。所以开头要先减一次循环变量。
                   TryGetForloopILimit 里面对溢出做了检测，可以保证整型情况不会溢出。但如果在循环中用浮点数极值，个人感觉可能会溢出？ */
                LuaValue initVal  = _stack[(int)ra];
                LuaValue limitVal = _stack[(int)ra + 1];
                LuaValue stepVal  = _stack[(int)ra + 2];
                if (initVal.IsInt && stepVal.IsInt &&
                    LuaVMUtils.TryGetILimit(limitVal, out long limit, stepVal.Int, out bool stopnow)) { /* 循环变量是安全的整型情况 */
                    /* 这个 stopnow 指的是循环极限是极值且 step 方向相反的情况。设为 0 是为了【安全执行】 FORLOOP 里是否超出范围的判断 */
                    long init           = stopnow ? 0 : initVal.Int;
                    _stack[(int)ra]     = new LuaValue(init - stepVal.Int);
                    _stack[(int)ra + 1] = new LuaValue(limit);
                } else { /* 循环变量是浮点数 */
                    if (!initVal.ToNumber(out double init))
                        throw new LuaRuntimeError("'for' initial value must be a number.");
                    if (!limitVal.ToNumber(out double nlimit))
                        throw new LuaRuntimeError("'for' limit must be a number.");
                    if (!stepVal.ToNumber(out double step))
                        throw new LuaRuntimeError("'for' step must be a number.");
                    _stack[(int)ra]     = new LuaValue(init - step);
                    _stack[(int)ra + 1] = new LuaValue(nlimit);
                    _stack[(int)ra + 2] = new LuaValue(step);
                }
                vmCI.PC += inst.sBx;
                break;
            }
            case OpCode.TFORCALL: /* 调用泛型 for 迭代器 | R(A+3), ... ,R(A+2+C) := R(A)(R(A+1), R(A+2)); */
            case OpCode.TFORLOOP: /* 泛型 for 判断是否结束 | if R(A+1) ~= nil then { R(A)=R(A+1); pc += sBx } */
            {
                /* OpCode.TFORCALL */
                if (inst.OpCode == OpCode.TFORCALL) {
                    /* 下面三个 setobjs2s 是把 _f, _s, _var 拷到新位置上，调用后会得到迭代器 _f 的返回值，原本的留下来作为【内部备份】，下次循环继续这个操作 */

                    /* +3 是为了给 _f, _s, _var 变量做内部备份 */
                    RawIdx callbase = ra + 3;

                    /* 初始化栈帧，压入迭代器和参数 */
                    _stack[(int)callbase + 2] = _stack[(int)ra + 2]; /* _var，上次迭代器返回值的，初始值由迭代器生成器提供 */
                    _stack[(int)callbase + 1] = _stack[(int)ra + 1]; /* _s，固定参数的，每次都会传给 _f */
                    _stack[(int)callbase]     = _stack[(int)ra];     /* _f，迭代器 */

                    /* 调用迭代器 */
                    _top              = callbase + 3;
                    short resultCount = (short)inst.C.RKValue;
                    _Call(callbase, resultCount);
                    _top = vmCI.Top;

                    /* 更新指令，下条指令一定是 TFORLOOP，然后顺延执行 */
                    inst = vmCI.GetInstruction(vmCI.PC++);
                    ra   = RA(inst);
                    LuaDebug.Assert(inst.OpCode == OpCode.TFORLOOP);
                }

                /* OpCode.TFORLOOP */
                if (!_stack[(int)ra + 1].IsNil) {          /* 如果迭代器第一个返回值不为 nil，则继续循环 */
                    _stack[(int)ra] = _stack[(int)ra + 1]; /* 将第一个返回值保存到 _var 的【内部备份】，会再传给 _f */
                    vmCI.PC += inst.sBx;                   /* 跳回 TFORCALL */
                }
                break;
            }
            case OpCode.SETLIST: {          /* 对缓冲表批量赋值 | R(A)[(C-1)*FPF+i] := R(A+i), 1 <= i <= B */
                int count = inst.B.RKValue; /* B 为本次写入的元素数量，当 B 为 0 时会自动获取（即，一直读到 top 为止） */
                int start = inst.C.RKValue; /* C 是表起始索引（xFPF 就得到实际索引），当 C 为 0 时，会从 EXTRAARG 读取起始索引 */
                if (count == 0)
                    count = _top - ra - 1;
                if (start == 0) {
                    LuaDebug.Assert(vmCI.NextInst.OpCode == OpCode.EXTRAARG);
                    start = vmCI.GetInstruction(vmCI.PC++).Ax; /* 注意，哪怕是从 EXTRAARG 读取，start 也是从 1 开始的 */
                }

                LuaTable table     = _stack[(int)ra].LObject<LuaTable>();
                LuaValue lastIndex = new LuaValue((start - 1) * LuaConfig.LFIELDS_PER_FLUSH + count);
                for (; count > 0; count--) {
                    table.Set(lastIndex, _stack[(int)ra + count]);
                    lastIndex._RefChangeValue(lastIndex.Int - 1);
                }
                _top = vmCI.Top;
                break;
            }
            case OpCode.CLOSURE: { /* 将 Proto 和上值绑定，创建新闭包 | R(A) := closure(KPROTO[Bx]) */
                LuaProto proto       = currClosure.proto.SubProtos[inst.Bx];
                LClosure newLClosure = LuaVMUtils.GetCached(proto, currClosure, firstArg); /* 尝试复用上一个闭包 */
                if (newLClosure == null)
                    _NewLClosureHelper(proto, currClosure, firstArg, ra);
                else
                    _stack[(int)ra] = new LuaValue(newLClosure);
                break;
            }
            case OpCode.VARARG: { /* 读取可变参数（...） | R(A), R(A+1), ..., R(A+B-2) = vararg */
                /* Lua 函数执行时，栈帧内存模型（从栈底到栈顶）是 func-param-vararg-framebase-fixedarg，具体看 _AdjustVarargs */
                int wantedCount = inst.B.RKValue - 1;
                int currCount   = firstArg - vmCI.Func - vmCI.LClosure.proto.ParamCount - 1;
                currCount       = Math.Max(currCount, 0);

                if (wantedCount < 0) { /* 设置 B 参数为 0 时，表示接收全部 vararg */
                    wantedCount = currCount;
                    _top        = ra + currCount;
                }

                int i = 0;
                for (; i < wantedCount && i < currCount; i++)
                    _stack[(int)ra + i] = _stack[(int)firstArg - currCount + i];
                for (; i < wantedCount; i++) /* 不足时用 nil 补 */
                    _stack[(int)ra + i] = LuaValue.NIL;
                break;
            }
            case OpCode.EXTRAARG: /* 上一条指令的拓展参数 | Ax */
                LuaDebug.Assert(false);
                break;
            }
        }
    }
    internal void FinishOp() {
        /* 结束一个被中断的指令。如果指令存在【中断点】（即，可能发生 yield 的地方），都要在这里写一下中断恢复逻辑
           通常来说就是把中断后的逻辑执行一遍，这里总是假设：“恢复中断 OP 时，中断函数已经执行成功” */
        CallInfo    currCI    = _currCI;
        RawIdx      frameBase = currCI.FirstArg;
        Instruction inst      = currCI.GetInstruction(currCI.PC - 1);
        switch (inst.OpCode) {
        case OpCode.ADD:
        case OpCode.SUB:
        case OpCode.MUL:
        case OpCode.DIV:
        case OpCode.IDIV:
        case OpCode.BAND:
        case OpCode.BOR:
        case OpCode.BXOR:
        case OpCode.SHL:
        case OpCode.SHR:
        case OpCode.MOD:
        case OpCode.POW:
        case OpCode.UNM:
        case OpCode.BNOT:
        case OpCode.LEN:
        case OpCode.GETTABUP:
        case OpCode.GETTABLE:
        case OpCode.SELF: {
            /* 这些指令的【中断点】都在最后执行元方法时，所以假设 resume 时结果已经在栈顶 */
            _top--;
            _stack[(int)frameBase + inst.A.RKValue] = _stack[(int)_top];
            break;
        }
        case OpCode.LE:
        case OpCode.LT:
        case OpCode.EQ: {
            /* OP_LE 有两个【中断点】（LE 或 LT 的元方法）；LT 和 EQ 只有一个【中断点】，也是元方法。同样都都假设 resume 时结果已压入栈 */
            bool result = _stack[(int)_top - 1].ToBoolean();
            _top--;
            if (currCI.GetCallStatusFlag(CallStatus.LEQ)) { /* "<=" using "<" instead? */
                /* 尝试用 LT 代替 LE，恢复时要再执行 luaV_lessequal 元方法后的步骤 */
                LuaDebug.Assert(inst.OpCode == OpCode.LE);
                currCI.SetCallStatusFlag(CallStatus.LEQ, false); /* clear mark */
                result = !result;                                /* 用 '<=' 代替 '<' 的话，结果要取反 */
            }
            LuaDebug.Assert(currCI.NextInst.OpCode == OpCode.JMP);
            if (result != (inst.A.RKValue != 0)) /* 条件测试；condition failed? */
                currCI.PC++;
            break;
        }
        case OpCode.CONCAT: {
            /* Concat 的恢复有点复杂，因为 Concat 并不是一次执行完的，它在一个【循环】里面。就算恢复后，仍可能被后续的元方法中断 */
            /* 此时情况是，CallBinTagMethod 发生了中断并恢复
               【中断前】内存布局是：item{1}...item{-2}, item{-1}, item{-2}, item{-1}, _top；后面两个元素重复，是因为 CallBinTagMethod 把参数压入栈中再调用；
               【恢复后】内存布局是：item{1}...item{-2}, item{-1}, result, _top；CallBinTagMethod 调用只有一个返回值 */
            RawIdx top           = _top - 1;                      /* 把 top 设为 result 位置上，这是 CallBinTagMethod 调用前的 top */
            int    start         = inst.B.RKValue;                /* 连接起点 */
            int    left          = top - 1 - (frameBase + start); /* 剩余待连接的数量 */
            _stack[(int)top - 2] = _stack[(int)top];              /* 把 result 设到正确位置，该步本应由 CallBinTagMethod 完成，但被中断了 */
            if (left > 1) {
                _top = top - 1; /* 还有剩余待连接元素，把 _top 设到正确位置，继续连接 */
                Concat(left);
            }
            /* 移动结果到 ra 上。当然，可能执行不到这里，Concat 还有可能挂起，就要等下一次再恢复 */
            _stack[(int)currCI.FirstArg + inst.A.RKValue] = _stack[(int)_top - 1];
            _top                                          = currCI.Top; /* 上面的寄存器是临时占用的，这里恢复栈帧；restore top */
            break;
        }
        case OpCode.TFORCALL: {
            /* 这里的中断点自然是迭代器，下条指令必须是 OP_TFORLOOP，这里只需要把 TFORCALL 剩下的代码执行一下即可 */
            LuaDebug.Assert(currCI.NextInst.OpCode == OpCode.TFORLOOP);
            _top = currCI.Top;
            /* i 和 ra 会在下一轮 Execute 时在 Fetch 里更新，这里不用更新 */
            break;
        }
        case OpCode.CALL: {
            /* 注意 TAILCALL 只是更改栈帧和 ci，并未发出任何调用，所以不会中断 */
            int resultCount = inst.C.RKValue - 1;
            if (resultCount >= 0) /* 只有 C 函数执行可能中断，Lua 函数的情况不需要处理（因为 PreCall 根本没发出调用） */
                _top = currCI.Top;
            break;
        }
        case OpCode.TAILCALL:
        case OpCode.SETTABUP:
        case OpCode.SETTABLE:
            break;
        default:
            LuaDebug.Assert(false);
            break;
        }
    }

    private void _NewLClosureHelper(LuaProto proto, LClosure curr, RawIdx firstArg, RawIdx output) {
        // LClosure lclosure   = new LClosure(this, proto, curr.UpvalueCount);
        LClosure lclosure   = new LClosure(proto, proto.UpvalueCount);
        _stack[(int)output] = new LuaValue(lclosure);
        /* 填充上值 */
        for (int i = 0; i < proto.UpvalueDescCount; i++) {
            UpvalueDesc upvalueDesc = proto.UpvalueDescList[i];
            if (upvalueDesc.InStack)
                lclosure.SetUpvalueObj(i + 1, FindewUpvalue(firstArg + upvalueDesc.Ldx - 1));
            else
                lclosure.SetUpvalueObj(i + 1, curr.GetUpvalueObj(upvalueDesc.Ldx));
        }
        proto.LClosureCache = lclosure;
    }

    private void _DoJump(CallInfo ci, Instruction jumpInst, int offset) {
        /* offset 通常就是【当前】指令到 jumpInst 的偏移。比如：当前 pc 指向 jumpInst 的话，那 offset 就为 1（因为 pc 指的是“下一条未读指令”） */
        int a = jumpInst.A.RKValue;
        if (a != 0)
            CloseUpvalues(ci.FirstArg + a - 1); /* 注意这里会关闭直到跳转指令的 a-1 的上值（不是 a），因为 0 被保留作为“不关闭”的含义 */
        ci.PC += jumpInst.sBx + offset;
    }

    private void _DoNextJump(CallInfo ci) {
        _DoJump(ci, ci.NextInst, 1); /* offset 为 1 是因为读取的是下一条指令作为跳转指令 */
    }
}

internal static class LuaVMUtils
{
    /// <summary>尝试将 LuaValue 格式的循环极限值转为整型</summary>
    /// <param name="limitValue">当前循环极限值</param>
    /// <param name="limit">循环极限的整型</param>
    /// <param name="step">步长</param>
    /// <param name="stopnow">是否应当立即结束循环</param>
    /// <returns></returns>
    public static bool TryGetILimit(in LuaValue limitValue, out long limit, long step, out bool stopnow) {
        stopnow = false;
        if (!limitValue.ToInteger(out limit, step < 0 ? ToIntMode.CEIL : ToIntMode.FLOOR)) {
            /* value 无法转为整型 */
            if (!limitValue.ToNumber(out double nlimit)) /* 无法转为浮点数 */
                return false;
            /* 可以转为浮点数，表示数值超出整型范围 */
            if (nlimit > 0) {
                limit   = long.MaxValue;
                stopnow = step < 0;
            } else {
                limit   = long.MinValue;
                stopnow = step >= 0;
            }
        }
        return true;
    }

    /// <summary>检查 proto 的闭包缓存是否还可重用（即，缓存的闭包的上值是否可以给新闭包使用）</summary>
    /// <param name="proto">目标 proto</param>
    /// <param name="curr">当前闭包，与返回值是包含关系，拥有返回值闭包的上值</param>
    /// <returns>如果可以重用则返回缓存的闭包，否则返回 null</returns>
    public static LClosure GetCached(LuaProto proto, LClosure curr, RawIdx firstArg) {
        LClosure cache = proto.LClosureCache;
        /* 线程和上值数量必须一样才能复用 */
        if (cache == null || proto.UpvalueDescCount != curr.UpvalueCount)
            return null;
        for (int i = 0; i < proto.UpvalueDescCount; i++) {
            UpvalueDesc upvalueDesc  = proto.UpvalueDescList[i];
            Upvalue     cacheUpvalue = cache.GetUpvalueObj(i + 1);
            if (!cacheUpvalue.Open)
                return null;
            if (upvalueDesc.InStack && cacheUpvalue.RawIdx != firstArg + upvalueDesc.Ldx - 1)
                return null;
            Upvalue currUpvalue = curr.GetUpvalueObj(upvalueDesc.Ldx);
            if (!currUpvalue.Open || currUpvalue.Thread != cacheUpvalue.Thread) /* 关于 Open 和 Instack 的区别，参考 Instack 属性的注释 */
                return null;
            if (!upvalueDesc.InStack && cacheUpvalue.RawIdx != currUpvalue.RawIdx)
                return null;
        }
        return cache;
    }
}

}
