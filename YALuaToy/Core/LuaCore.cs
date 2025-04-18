namespace YALuaToy.Core {

using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Compilation;
using YALuaToy.Compilation.Antlr;
using Antlr4.Runtime;

public delegate int LuaCFunction(LuaState state);
/* LuaKFunction 只能用参数 status 来判断线程实际状态（具体看 Resume 代码，恢复时线程状态可能处于 OK 但实际执行失败）；
   连续函数的设计建议：如果使用了连续函数，那么 Call 或 PCall 后的逻辑也应该由连续函数实现（参考 LuaBaseLib._DoFile） */
public delegate int LuaKFunction(LuaState state, ThreadStatus status, IntPtr ctx);

public partial class LuaState
{
    public LuaCFunction SetPanic(LuaCFunction panicFunc) {
        return globalState.SetPanic(panicFunc);
    }

    public double LuaVersion => LuaConst.VERSION_NUM;

    /* ---------------- Stack Manipulation ---------------- */

    public int TopLdx { /* 注意这个 "Top" 就是指最高元素的位置（而不是下个位置），也可以理解为当前栈元素数量 */
        get {
            return _top - _currCI.Func - 1;
        }
        set {
            if (value >= 0) {
                LuaDebug.Check(value <= _last - _currCI.Func - 1, $"New top too large: {value}, last: {_last}, func: {_currCI.Func}.");
                while (_top < _currCI.Func + value + 1)
                    _stack[(int)_top++] = LuaValue.NIL;
                _top = _currCI.Func + value + 1;
            } else {
                /* 这里要先 +1，因为 -1 表示栈顶元素，当 value==-1 时实际上不用移除任何元素 */
                LuaDebug.Check(-(value + 1) <= _top - _currCI.Func - 1, $"New top too small: {value}, top: {_top}, func: {_currCI.Func}.");
                _top += value + 1;
            }
        }
    }
    public void PushValue(int ldx) {
        _stack[(int)_top] = _Get(ldx);
        IncreaseTop();
    }
    public void Rotate(int ldx, int n) {
        RawIdx end = _top - 1;
        bool success = ToRawIdx(ldx, out RawIdx start); /* 注意写法，不要把对环境有影响的代码写到 Assert 里面。。。Release 时会被剔除 */
        LuaDebug.Assert(success, $"Invalid stack index: {ldx}");
        CheckValidElem(_stack[(int)start]);
        LuaDebug.Check(Math.Abs(n) <= end - start + 1, $"Invalid rotate count: {n}");
        RawIdx mid = n >= 0 ? end - n : start - n - 1;
        _Reverse(start, mid);
        _Reverse(mid + 1, end);
        _Reverse(start, end);
    }
    public void Copy(int fromLdx, int toLdx) {
        CheckValidElem(_Get(toLdx));
        _Set(toLdx, _Get(fromLdx));
    }
    public void XMove(LuaState to, int elemCount) {
        if (this == to)
            return;
        CheckArgOrResultCount(elemCount);
        LuaDebug.Assert(globalState == to.globalState, "Can't move between different state.");
        LuaDebug.Assert(
            to._currCI.Top - to._top >= elemCount,
            $"Target LuaState stack overflow, toCITop: {to._currCI.Top}, toTop: {to._top}, elemCount: {elemCount}."
        );
        _top -= elemCount;
        for (int i = 0; i < elemCount; i++) {
            to._stack[(int)to._top] = _stack[(int)_top + i];
            to._top++;
        }
    }
    public void Pop(int n = 1) {
        TopLdx = -n - 1;
    }
    public void Insert(int ldx) {
        /* 把栈顶元素插入到 ldx 位置 */
        Rotate(ldx, 1);
    }
    public void Remove(int ldx) {
        Rotate(ldx, -1);
        Pop();
    }
    public void Replace(int ldx) {
        Copy(-1, ldx);
        Pop();
    }
    public void ClearFrame() { /* 直接清空栈帧 */
        TopLdx = 0;
    }

    public int ToAbsLdx(int ldx) {
        /* 还要考虑伪索引。处于性能考虑不做范围合法性判断，可以调用 IsValidStackLdx 判断 */
        return (ldx > 0 || IsPseudoLdx(ldx)) ? ldx : _top - _currCI.Func + ldx;
    }
    public bool LuaCheckStack(int needSpace) {
        void DoGrowStack(LuaState state, IntPtr ud) {
            state.GrowStack(checked((int)ud));
        }
        LuaDebug.Assert(needSpace >= 0, $"Need space must be non-negative: {needSpace}.");
        bool success = false;
        if (_last - _top > needSpace) {
            success = true;
        } else if ((int)_top + LuaConfig.EXTRA_STACK <= LuaConfig.LUAI_MAXSTACK - needSpace) {
            success = _RawPCall(DoGrowStack, needSpace) == ThreadStatus.OK;
        }
        if (success && _currCI.Top < _top + needSpace)
            _currCI.Top = _top + needSpace; /* adjust frame top */
        return success;
    }

    /* ---------------- Access (Stack -> C) ---------------- */

    public sbyte GetType(int ldx) {
        /* 只返回用户可见的 tag，这里有可能返回 None 表示没有值 */
        LuaValue value = _Get(ldx);
        return value.Type.Tag;
    }
    public static string TypeName(sbyte typeTag) {
        /* 只能处理无变种的情况 */
        LuaDebug.Assert(typeTag < LuaConst.TOTALTAGS, $"Invalid type tag: {typeTag}");
        return LuaConst.TypeName(typeTag);
    }
    public bool IsNumber(int ldx) {
        /* 这个连字符串都可能返回 true。。。也是 Lua 最让人讨厌的地方之一 */
        return _Get(ldx).ToNumber(out _);
    }
    public bool IsString(int ldx) {
        LuaValue value = _Get(ldx);
        return value.IsString || value.CanConvertToString;
    }
    public bool IsCFunction(int ldx) {
        return LuaType.CheckVariant(_Get(ldx), LuaConst.TLCF, LuaConst.TCCL);
    }
    public bool IsInteger(int ldx) {
        return _Get(ldx).IsInt;
    }
    public bool IsUserData(int ldx) { /* 包括 light userdata 也返回 true */
        return LuaType.CheckTag(_Get(ldx), LuaConst.TUSERDATA, LuaConst.TLIGHTUSERDATA);
    }
    public bool IsFunction(int ldx) {
        return GetType(ldx) == LuaConst.TFUNCTION;
    }
    public bool IsTable(int ldx) {
        return GetType(ldx) == LuaConst.TTABLE;
    }
    public bool IsLightUserData(int ldx) {
        return GetType(ldx) == LuaConst.TLIGHTUSERDATA;
    }
    public bool IsNil(int ldx) {
        return GetType(ldx) == LuaConst.TNIL;
    }
    public bool IsBoolean(int ldx) {
        return GetType(ldx) == LuaConst.TBOOLEAN;
    }
    public bool IsThread(int ldx) {
        return GetType(ldx) == LuaConst.TTHREAD;
    }
    public bool IsNone(int ldx) {
        return GetType(ldx) == LuaConst.TNONE;
    }
    public bool IsNoneOrNil(int ldx) {
        return IsNone(ldx) || GetType(ldx) == LuaConst.TNIL;
    }

    public double ToNumber(int ldx) {
        return ToNumber(ldx, out _);
    }
    public double ToNumber(int ldx, out bool success) {
        LuaValue value = _Get(ldx);
        success        = value.ToNumber(out double result);
        return result;
    }
    public long ToInteger(int ldx) {
        return ToInteger(ldx, out _);
    }
    public long ToInteger(int ldx, out bool success) {
        LuaValue value = _Get(ldx);
        success        = value.ToInteger(out long result);
        return result;
    }
    public bool ToBoolean(int ldx) {
        return _Get(ldx).ToBoolean();
    }
    public string ToString(int ldx, out bool success) {
        success        = false;
        LuaValue value = _Get(ldx);
        if (value.IsString) {
            success = true;
            return value.Str;
        } else if (value.CanConvertToString) {
            success = value.ToString(out string str);
            if (success) {
                _Set(ldx, new LuaValue(str));
                return str;
            }
        }
        return null;
    }
    public LuaCFunction ToCFunction(int ldx) {
        LuaValue value = _Get(ldx);
        if (value.Type.NotNoneVariant == LuaConst.TLCF) {
            return value.LightFunc;
        } else if (value.Type.NotNoneVariant == LuaConst.TCCL) {
            return value.LObject<CClosure>().func;
        }
        return null;
    }
    public LuaState ToThread(int ldx) {
        LuaValue value = _Get(ldx);
        return value.IsThread ? value.LObject<LuaState>() : null;
    }
    public long ToId(int ldx) {
        /* 只能用于调试和标识，基于地址的 GetHashCode 实现。C# 的对象没有固定地址，所以无法保证完全唯一。
           也许用句柄可以做到完全唯一，但可能有超接口出调用者预期的消耗。考虑到用途有限就先这么实现了 */
        LuaValue value = _Get(ldx);
        if (value.IsLuaObject)
            return RuntimeHelpers.GetHashCode(value.Object);
        if (value.Type.Tag == LuaConst.TLIGHTUSERDATA)
            return value.LightUserData;
        else if (value.Type.Variant == LuaConst.TLCF)
            return RuntimeHelpers.GetHashCode(value.LightFunc);
        return 0;
    }

    /* ---------------- Push (C -> Stack) ---------------- */

    public void Push(double d) {
        PushStack(d);
    }
    public void Push(int i) {
        PushStack((long)i);
    }
    public void Push(long i) {
        PushStack(i);
    }
    public void Push(bool b) {
        PushStack(b ? LuaValue.TRUE : LuaValue.FALSE);
    }
    public void Push(LuaCFunction func, int upvalueCount = 0) {
        if (upvalueCount == 0) {
            PushStack(func);
        } else {
            CheckArgOrResultCount(upvalueCount);
            LuaDebug.AssertValidUpvalueLdx(upvalueCount);
            CClosure cclosure = new CClosure(func, upvalueCount);
            _top -= upvalueCount;
            for (; upvalueCount > 0; upvalueCount--)
                cclosure.SetUpvalue(upvalueCount, _stack[(int)_top + upvalueCount - 1]);
            PushStack(cclosure);
        }
    }
    public void Push(IntPtr l) {
        PushStack(l);
    }
    public string Push(string s) {
        LuaValue value = s == null ? LuaValue.NIL : new LuaValue(s);
        PushStack(value);
        return s;
    }
    public string Push<T1>(string s, T1 arg1) {
        return Push(LuaUtils.FormatString(s, arg1));
    }
    public string Push<T1, T2>(string s, T1 arg1, T2 arg2) {
        return Push(LuaUtils.FormatString(s, arg1, arg2));
    }
    public string Push<T1, T2, T3>(string s, T1 arg1, T2 arg2, T3 arg3) {
        return Push(LuaUtils.FormatString(s, arg1, arg2, arg3));
    }
    public string Push<T1, T2, T3, T4>(string s, T1 arg1, T2 arg2, T3 arg3, T4 arg4) {
        return Push(LuaUtils.FormatString(s, arg1, arg2, arg3, arg4));
    }
    public string Push<T1, T2, T3, T4, T5>(string s, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) {
        return Push(LuaUtils.FormatString(s, arg1, arg2, arg3, arg4, arg5));
    }

    public void PushNil() {
        PushStack(LuaValue.NIL);
    }
    public bool PushSelf() {
        PushStack(this);
        return isMainThread;
    }
    public void PushGlobalTable() {
        RawGetTable(LuaConst.REGISTRYINDEX, LuaConst.RIDX_GLOBALS);
    }

    public void Register(string name, LuaCFunction func) {
        Push(func);
        SetGlobal(name);
    }

    /* ---------------- Get (Lua -> Stack) ---------------- */

    public sbyte GetGlobal(string name) {
        LuaTable registry = globalState.Registry;
        LuaValue global   = registry.Get(new LuaValue(LuaConst.RIDX_GLOBALS));
        LuaValue value    = Index(global, new LuaValue(name));
        PushStack(value);
        return value.Type.Tag;
    }
    public sbyte GetTable(int tableLdx) {
        LuaValue table        = _Get(tableLdx);
        _stack[(int)_top - 1] = Index(table, _stack[(int)_top - 1]);
        return _stack[(int)_top - 1].Type.Tag;
    }
    public sbyte GetTable(int tableLdx, string key) {
        LuaValue table = _Get(tableLdx);
        LuaValue value = Index(table, new LuaValue(key));
        PushStack(value);
        return value.Type.Tag;
    }
    public sbyte GetTable(int tableLdx, long key) {
        LuaValue table = _Get(tableLdx);
        LuaValue value = Index(table, new LuaValue(key));
        PushStack(value);
        return value.Type.Tag;
    }
    public sbyte RawGetTable(int tableLdx) {
        _Get(tableLdx).LObject(out LuaTable table);
        _stack[(int)_top - 1] = table.Get(_stack[(int)_top - 1]);
        return _stack[(int)_top - 1].Type.Tag;
    }
    public sbyte RawGetTable(int tableLdx, string key) {
        _Get(tableLdx).LObject(out LuaTable table);
        LuaValue value = table.Get(new LuaValue(key));
        PushStack(value);
        return value.Type.Tag;
    }
    public sbyte RawGetTable(int tableLdx, int key) {
        return RawGetTable(tableLdx, (long)key);
    }
    public sbyte RawGetTable(int tableLdx, long key) {
        _Get(tableLdx).LObject(out LuaTable table);
        LuaValue value = table.Get(new LuaValue(key));
        PushStack(value);
        return value.Type.Tag;
    }
    public sbyte RawGetTable(int tableLdx, IntPtr key) { /* lightuserdata 作为索引 */
        _Get(tableLdx).LObject(out LuaTable table);
        LuaValue value = table.Get(new LuaValue(key));
        PushStack(value);
        return value.Type.Tag;
    }

    public void NewTable() {
        PushStack(new LuaTable());
    }
    public bool GetMetatable(int ldx) {
        LuaTable metatable;
        LuaValue value = _Get(ldx);
        switch (value.Type.NotNoneTag) {
        case LuaConst.TTABLE:
            metatable = value.LObject<LuaTable>().Metatable;
            break;
        case LuaConst.TUSERDATA:
            metatable = value.LObject<LuaUserData>().Metatable;
            break;
        default:
            metatable = globalState.MetaTables[value.Type.NotNoneTag];
            break;
        }
        if (metatable != null) {
            PushStack(metatable);
            return true;
        }
        return false;
    }
    public sbyte GetUserValue(int ldx) {
        throw new NotImplementedException(); /* lua_getuservalue */
    }

    /* ---------------- Set (Stack -> Lua) ---------------- */

    public void SetGlobal(string name) {
        CheckArgOrResultCount(1);
        LuaTable registry = globalState.Registry;
        LuaValue global   = registry.Get(new LuaValue(LuaConst.RIDX_GLOBALS));
        NewIndex(global, new LuaValue(name), _stack[(int)_top - 1]);
        _top--;
    }
    public void SetTable(int tableLdx) {
        CheckArgOrResultCount(2);
        LuaValue table = _Get(tableLdx);
        NewIndex(table, _stack[(int)_top - 2], _stack[(int)_top - 1]);
        _top -= 2;
    }
    public void SetTable(int tableLdx, string key) {
        CheckArgOrResultCount(1);
        LuaValue table = _Get(tableLdx);
        NewIndex(table, new LuaValue(key), _stack[(int)_top - 1]);
        _top -= 1;
    }
    public void SetTable(int tableLdx, long key) {
        CheckArgOrResultCount(1);
        LuaValue table = _Get(tableLdx);
        NewIndex(table, new LuaValue(key), _stack[(int)_top - 1]);
        _top -= 1;
    }
    public void RawSetTable(int tableLdx) {
        CheckArgOrResultCount(2);
        _Get(tableLdx).LObject(out LuaTable table);
        table.Set(_stack[(int)_top - 2], _stack[(int)_top - 1]);
        table._InvalidateFlags();
        _top -= 2;
    }
    public void RawSetTable(int tableLdx, string key) {
        CheckArgOrResultCount(1);
        _Get(tableLdx).LObject(out LuaTable table);
        table.Set(new LuaValue(key), _stack[(int)_top - 1]);
        table._InvalidateFlags();
        _top--;
    }
    public void RawSetTable(int tableLdx, int key) {
        RawSetTable(tableLdx, (long)key);
    }
    public void RawSetTable(int tableLdx, long key) {
        CheckArgOrResultCount(1);
        _Get(tableLdx).LObject(out LuaTable table);
        table.Set(new LuaValue(key), _stack[(int)_top - 1]);
        _top--;
    }
    public void RawSetTable(int tableLdx, IntPtr key) {
        CheckArgOrResultCount(1);
        _Get(tableLdx).LObject(out LuaTable table);
        table.Set(new LuaValue(key), _stack[(int)_top - 1]);
        _top--;
    }

    public void SetMetatable(int tableLdx) {
        CheckArgOrResultCount(1);
        LuaValue tableValue = _Get(tableLdx);

        LuaTable metatable;
        if (_stack[(int)_top - 1].IsNil)
            metatable = null;
        else {
            LuaDebug.AssertTag(_stack[(int)_top - 1], LuaConst.TTABLE);
            metatable = _stack[(int)_top - 1].LObject<LuaTable>();
        }

        switch (tableValue.Type.NotNoneTag) {
        case LuaConst.TTABLE:
            tableValue.LObject<LuaTable>().Metatable = metatable;
            break;
        case LuaConst.TUSERDATA:
            tableValue.LObject<LuaUserData>().Metatable = metatable;
            break;
        default:
            globalState.MetaTables[tableValue.Type.NotNoneTag] = metatable;
            break;
        }
        _top--;
    }
    public void SetUserValue(int ldx) {
        throw new NotImplementedException(); /* lua_setuservalue */
    }

    /* ---------------- Operation ---------------- */

    public int RawLength(int ldx) {
        LuaValue value = _Get(ldx);
        if (value.IsString) {
            return value.LObject<LuaString>().UTF8Length;
        } else if (value.Type.NotNoneTag == LuaConst.TUSERDATA) {
            return value.LObject<LuaUserData>().Length;
        } else if (value.IsTable) {
            return value.LObject<LuaTable>().GetArrayLength();
        }
        return 0;
    }
    public bool RawEquals(int ldx1, int ldx2) {
        LuaValue value1 = _Get(ldx1);
        LuaValue value2 = _Get(ldx2);
        return (value1.Valid && value2.Valid) ? value1.Equals(in value2) : false;
    }
    public bool Compare(Comp op, int ldx1, int ldx2) {
        LuaValue lhs = _Get(ldx1);
        LuaValue rhs = _Get(ldx2);
        if (lhs.Valid && rhs.Valid) {
            switch (op) {
            case Comp.EQ:
                return Equals(lhs, rhs);
            case Comp.LT:
                return LessThan(lhs, rhs);
            case Comp.LE:
                return LessEqual(lhs, rhs);
            }
        }
        LuaDebug.AssertValidEnum(op);
        return false;
    }
    public void Arith(Op op) {
        if (op == Op.UNM || op == Op.BNOT) {
            CheckArgOrResultCount(1);
            _stack[(int)_top] = _stack[(int)_top - 1];
            IncreaseTop();
        } else {
            CheckArgOrResultCount(2);
        }
        Arith(op, _stack[(int)_top - 2], _stack[(int)_top - 1], _top - 2);
        _top--;
    }

    /* ---------------- Load and Call ---------------- */

    public void Call(short argCount, short resultCount, IntPtr ctx, LuaKFunction k) {
        LuaDebug.Check(k == null || !_currCI.IsLua, "Cannot use continuations inside hooks.");
        CheckArgOrResultCount(argCount + 1); /* 这里 +1 是因为要先压入一个函数（由于还未建立新栈帧，所以不会被 Assert 忽略） */
        LuaDebug.Check(_threadStatus == ThreadStatus.OK, $"Cannot call function in status: {_threadStatus}");
        _AssertCanReturn(argCount, resultCount);
        RawIdx func = _top - (argCount + 1);
        if (k != null && Yieldable) {
            _currCI.SetContinuation(k, ctx);
            _Call(func, resultCount);
        } else {
            _Call(func, resultCount, noYield: true);
        }
        _AdjustResult(resultCount);
    }
    public void Call(short argCount, short resultCount) {
        Call(argCount, resultCount, IntPtr.Zero, null);
    }
    public ThreadStatus PCall(short argCount, short resultCount, int errorFuncLdx, IntPtr ctx, LuaKFunction k) {
        /* errorFuncLdx 为 0 表示没有错误处理函数，k 为 null 表示没有连续函数 */
        LuaDebug.Check(k == null || !_currCI.IsLua, "Cannot use continuations inside hooks.");
        CheckArgOrResultCount(argCount + 1); /* + 1 理由同上 */
        LuaDebug.Check(_threadStatus == ThreadStatus.OK, $"Cannot call function in status: {_threadStatus}");
        _AssertCanReturn(argCount, resultCount);

        ThreadStatus threadStatus;

        if (ToRawIdx(errorFuncLdx, out RawIdx errorFunc)) {
            CheckStackLdxAndElem(errorFuncLdx, _Get(errorFuncLdx));
        } else {
            errorFunc = RawIdx.InvalidErrorFunc;
            Debug.WriteLine($"[CORE WARNING] error func should be on stack: {errorFuncLdx}");
        }

        /* 执行闭包 */
        RawIdx func = _top - (argCount + 1);
        void   DoCall(LuaState state, IntPtr ctx_) {
            state._Call(func, resultCount, noYield: true);
        }

        if (k == null || !Yieldable) {
            threadStatus = _PCall(DoCall, IntPtr.Zero, func, errorFunc); /* old top 就是当前压入的第一个元素（即将要调用的函数） */
        } else {
            /* 此时一定处于 resume 内，因为 Yiedable 一定为 true */
            _currCI.ResetHostInfo(k, _errorFunc, ctx); /* 线程当前的 errorFunc 记录到 CI 到 oldErrorFunc 上 */
            _currCI.extra = func;                      /* 为 resume 备份 oldtop */

            _errorFunc = errorFunc;
            _currCI.SetCallStatusFlag(CallStatus.YIELDABLE_PCALL, true);
            _Call(func, resultCount); /* 由于 resume 已提供保护调用，所以这里用 Call */
            _currCI.SetCallStatusFlag(CallStatus.YIELDABLE_PCALL, false);
            _errorFunc = _currCI.OldErrorFunc;

            threadStatus = ThreadStatus.OK;
        }

        _AdjustResult(resultCount);
        return threadStatus;
    }
    public ThreadStatus PCall(short argCount, short resultCount, int errorFuncLdx) {
        return PCall(argCount, resultCount, errorFuncLdx, IntPtr.Zero, null);
    }

    public ThreadStatus Load(TextReader reader, string source) {
        AntlrInputStream inputStream = new AntlrInputStream(reader);
        return LuaParserUtils.Parse(this, inputStream, source);
    }
    public ThreadStatus Load(BinaryReader reader) {
        throw new NotImplementedException(); /* lua_load */
    }
    public ThreadStatus Dump(TextWriter writer) {
        throw new NotImplementedException(); /* lua_dump */
    }
    public ThreadStatus Dump(BinaryWriter writer) {
        throw new NotImplementedException(); /* lua_dump */
    }

    /* ---------------- Coroutine Manipulation ---------------- */
    /* yield 和 resume 在 LuaExecution 里 */

    /* ---------------- Utils ---------------- */

    public void Error() { /* 正常报错直接用 throw 即可，如果想用栈上对象来报错，就用可以这个 */
        LuaValue value = _Get(-1);
        throw new LuaRuntimeError(value);
    }
    public bool Next(int tableLdx) {
        /* 这个 Next 的实现需要一点技巧：在 LuaTable 上存放一个迭代器“缓存”。
           如果压入的是 nil，则直接缓存默认迭代器对象；如果压入的不是 nil，则要遍历直到遇到对应的 key；
           如果字典发生修改，迭代器缓存失效；如果调用 Next 时，top 位置的 key 和缓存的迭代器的 key 不一样，迭代器失效（要重新索引获取）
           该实现在连续遍历时速度较快，但如果从中途遍历效率就低很多 */
        _Get(tableLdx).LObject(out LuaTable table);
        LuaValue currkey = _stack[(int)_top - 1];
        if (table.GetEnumeratorNext(currkey, out var enumerator)) {
            Pop();
            PushStack(enumerator.Current.Key);
            PushStack(enumerator.Current.Value);
            return true;
        } else {
            _top--;
            return false;
        }
    }
    public void LuaConcat(int n) {
        CheckArgOrResultCount(n);
        if (n >= 2) {
            Concat(n);
        } else if (n == 0) {
            PushStack("");
        }
    }
    public void LuaGetLength(int ldx) {
        GetLength(_Get(ldx), _top);
        IncreaseTop();
    }
    public int StringToNumber(string s) {
        int consumedCharCount = LuaUtils.StringToLuaNumber(s, out LuaValue result);
        if (consumedCharCount > 0)
            PushStack(result);
        return consumedCharCount;
    }

    public string GetUpvalue(int funcLdx, int upvalueLdx) {
        LuaValue value       = _Get(funcLdx);
        string   upvalueName = _GetUpvalueHelper(value, upvalueLdx, out Closure closure, out LuaValue result);
        if (upvalueName != null)
            PushStack(result);
        return upvalueName;
    }
    public string SetUpvalue(int funcLdx, int upvalueLdx) {
        CheckArgOrResultCount(1);
        LuaValue value       = _Get(funcLdx);
        string   upvalueName = _GetUpvalueHelper(value, upvalueLdx, out Closure closure, out _);
        if (upvalueName != null) {
            LuaValue newUpvalue = _stack[(int)_top - 1];
            _top--;
            if (closure is CClosure cclosure)
                cclosure.SetUpvalue(upvalueLdx, newUpvalue);
            else {
                LClosure lclosure = (LClosure)closure;
                lclosure.SetUpvalue(upvalueLdx, newUpvalue);
            }
        }
        return upvalueName;
    }
    public int GetUpvalueId(int funcLdx, int upvalueLdx) {
        /* 这里直接返回哈希值（RuntimeHelpers.GetHashCode），对于 C Closure 同一个位置的上值写入后 id 依旧不变，这参考了 CLua */
        LuaValue closureValue = _Get(funcLdx);
        switch (closureValue.Type.NotNoneVariant) {
        case LuaConst.TLCL:
            LClosure lclosure = closureValue.LObject<LClosure>();
            LuaDebug.Assert(lclosure.IsValidUpvalueLdx(upvalueLdx), $"Invalid upvalue index: {upvalueLdx}");
            return RuntimeHelpers.GetHashCode(lclosure.GetUpvalueObj(upvalueLdx));
        case LuaConst.TCCL:
            CClosure cclosure = closureValue.LObject<CClosure>();
            LuaDebug.Check(cclosure.IsValidUpvalueLdx(upvalueLdx), $"Invalid upvalue index: {upvalueLdx}");
            return RuntimeHelpers.GetHashCode(cclosure) + upvalueLdx; /* 凑合着用吧 >_< */
        }
        LuaDebug.AssertVariant(closureValue, LuaConst.TLCL, LuaConst.TCCL);
        return 0;
    }
    public void JoinUpvalue(int funcLdx1, int upvalueLdx1, int funcLdx2, int upvalueLdx2) {
        _Get(funcLdx1).LObject(out LClosure lclosure1);
        _Get(funcLdx2).LObject(out LClosure lclosure2);
        Upvalue upvalue1 = lclosure1.GetUpvalueObj(upvalueLdx1);
        Upvalue upvalue2 = lclosure2.GetUpvalueObj(upvalueLdx2);
        if (upvalue1 == upvalue2)
            return;
        lclosure1.SetUpvalueObj(upvalueLdx1, upvalue2);
    }
}

/* 一些辅助实现的 private 函数放这里 */
public partial class LuaState
{
    /* 仅限于当前 CI 持续时间内有效，CI 退出后再使用是未定义行为 */
    internal struct StackPtr : IPointer
    {
        private WeakReference<LuaState> _stateWRef;
        public readonly int             ldx;

        public StackPtr(LuaState state, int ldx) {
            _stateWRef = new WeakReference<LuaState>(state);
            this.ldx   = ldx;
        }

        public LuaValue Value {
            get {
                _stateWRef.TryGetTarget(out LuaState state);
                return state._Get(ldx);
            }
            set {
                _stateWRef.TryGetTarget(out LuaState state);
                state._Set(ldx, value);
            }
        }
        public bool Valid => _stateWRef.TryGetTarget(out LuaState _);
    }

    /* 相比 GetStack、_stack[ldx] 这种操作，会考虑合索引法性以及 pseudo index 等情况 */
    internal LuaValue _Get(int ldx) {
        if (ldx > 0 || !IsPseudoLdx(ldx)) {
            if (ToRawIdx(ldx, out RawIdx rawIdx))
                return _stack[(int)rawIdx];
            else {
                /* ldx 对应的索引允许大于 _top 但不能 >= _currCI.Top */
                LuaDebug.Assert(rawIdx < _currCI.Top, $"Invalid stack index: {ldx}");
                return LuaValue.NONE;
            }
        } else if (ldx == LuaConst.REGISTRYINDEX) {
            return new LuaValue(globalState.Registry);
        } else {                                               /* upvalue */
            ldx                = LuaConst.REGISTRYINDEX - ldx; /* 上值索引，正数，从 1 开始 */
            LuaValue funcValue = _currCI.FuncValue;
            LuaDebug.AssertValidUpvalueLdx(ldx);
            LuaDebug.AssertVariant(funcValue, LuaConst.TLCF, LuaConst.TCCL);
            if (LuaType.CheckVariant(funcValue, LuaConst.TLCF)) { /* light c function */
                return LuaValue.NONE;
            } else {
                CClosure cclosure = funcValue.LObject<CClosure>();
                return cclosure.IsValidUpvalueLdx(ldx) ? cclosure.GetUpvalue(ldx) : LuaValue.NONE;
            }
        }
    }
    internal void _Set(int ldx, in LuaValue value) {
        if (ldx > 0 || !IsPseudoLdx(ldx)) {
            bool success = ToRawIdx(ldx, out RawIdx rawIdx);
            LuaDebug.Assert(success, $"Invalid stack index: {ldx}");
            if (rawIdx >= _top)
                throw new LuaCoreError($"Invalid stack index: {ldx}");
            else
                _stack[(int)rawIdx] = value;
        } else if (ldx == LuaConst.REGISTRYINDEX) {
            throw new LuaCoreError($"Can't overwrite registry: {ldx}");
        } else {                                               /* upvalue */
            ldx                = LuaConst.REGISTRYINDEX - ldx; /* 上值索引，正数，从 1 开始 */
            LuaValue funcValue = _currCI.FuncValue;
            LuaDebug.AssertValidUpvalueLdx(ldx);
            LuaDebug.AssertVariant(funcValue, LuaConst.TLCF, LuaConst.TCCL);
            if (LuaType.CheckVariant(funcValue, LuaConst.TLCF)) { /* light c function */
                throw new LuaCoreError($"Light c function has no upvalue: {ldx}");
            } else {
                CClosure cclosure = funcValue.LObject<CClosure>();
                if (!cclosure.IsValidUpvalueLdx(ldx))
                    throw new LuaCoreError($"Upvalue index too large: {ldx}");
                cclosure.SetUpvalue(ldx, value);
            }
        }
    }

    private void _Reverse(RawIdx from, RawIdx to) {
        for (; from < to; from++, to--) {
            LuaValue tmp      = _stack[(int)from];
            _stack[(int)from] = _stack[(int)to];
            _stack[(int)to]   = tmp;
        }
    }

    private string _GetUpvalueHelper(in LuaValue closureValue, int upvalueLdx, out Closure closure, out LuaValue result) {
        closure = null;
        result  = LuaValue.NONE;

        switch (closureValue.Type.NotNoneVariant) {
        case LuaConst.TCCL: /* C Closure */
            CClosure cclosure = closureValue.LObject<CClosure>();
            if (!cclosure.IsValidUpvalueLdx(upvalueLdx))
                return null;
            closure = cclosure;
            result  = cclosure.GetUpvalue(upvalueLdx);
            return "";
        case LuaConst.TLCL: /* Lua Closure */
            LClosure lclosure = closureValue.LObject<LClosure>();
            if (!lclosure.IsValidUpvalueLdx(upvalueLdx))
                return null;
            closure = lclosure;
            result  = lclosure.GetUpvalue(upvalueLdx);
            return lclosure.proto.UpvalueDescList[upvalueLdx - 1].Name;
        }
        return null;
    }

    [Conditional("DEBUG")]
    private void _AssertCanReturn(int argCount, int resultCount) {
        /* 这里假设该断言发生在调用前，且参数已压入栈 */
        LuaDebug.Assert(
            resultCount == LuaConst.MULTRET || _currCI.Top - _top >= resultCount - argCount,
            $"Not enough stack space for results. result: {resultCount}, arg: {argCount}, top: {_top}, funcTop: {_currCI.Top}"
        );
    }
}
}
