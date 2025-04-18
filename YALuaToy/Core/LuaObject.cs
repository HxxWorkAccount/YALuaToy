namespace YALuaToy.Core {

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using YALuaToy.Const;
using YALuaToy.Debug;
using System.Runtime.CompilerServices;

/* ================== Lua Value Type ================== */

public abstract class LuaObject : IEquatable<LuaObject>
{
    internal LuaType _type;
    internal LuaType Type => _type;

    /*
     * 注意，自带的 Equals 方法即是 CLua 中的 rawequal，想用 Lua 里的等价性判断应该用 LuaState.Equals
     * Lua 中要判断两个值是否相等时，要考虑元表的判断（包括基元类型也要考虑元表，具体参考 lua.h 的 lua_rawequal 和 lua_compare）
     * 下面这里实现的 Equals，是用于给 C# 字典用的比较，不是 LuaValue 的比较，
     * 技术上来说，HashCode 和 Equals 这两个方法，除非派生类具有不变性（如 LuaString），否则没必要重写（直接用地址做哈希和等价性比较即可）
     */
    protected virtual int HashCode() {
        return RuntimeHelpers.GetHashCode(this);
    }
    public virtual bool Equals(LuaObject other) {
        if (ReferenceEquals(other, null))
            return false;
        return _type.Raw == other._type.Raw && ReferenceEquals(this, other);
    }

    public override int GetHashCode() {
        return HashCode();
    }
    public override bool Equals(object obj) {
        if (obj is LuaObject other)
            return Equals(other);
        return false;
    }

    public static bool operator ==(LuaObject left, LuaObject right) {
        if (ReferenceEquals(left, right))
            return true;
        if (ReferenceEquals(left, null))
            return false;
        return left.Equals(right);
    }
    public static bool operator !=(LuaObject left, LuaObject right) {
        return !(left == right);
    }
}

/* LuaValue 有三种空状态：nil, none, invalid weak。对用户来说，只有 nil；对 Core 来说 none 表示没有值；invalid weak 是弱表需要考虑的情况，其他地方无感 */
internal struct LuaValue : IEquatable<LuaValue>
{
    public static readonly LuaValue      NIL               = new LuaValue((LuaType)LuaConst.TNIL);
    public static readonly LuaValue      NONE              = new LuaValue((LuaType)LuaConst.TNONE);
    public static readonly LuaValue      TRUE              = new LuaValue(true);
    public static readonly LuaValue      FALSE             = new LuaValue(false);
    public static readonly LuaValue      ZERO              = new LuaValue(0);
    public static readonly LuaValue      ONE               = new LuaValue(1);
    private static readonly LuaCFunction compareByHashMark = (s) => { return 0; }; /* 仅供弱表中使用，一个标记 */

    [StructLayout(LayoutKind.Explicit)]
    private struct Primitive
    {
        [FieldOffset(0)]
        public IntPtr l; /* light user data */

        [FieldOffset(0)]
        public bool b;

        [FieldOffset(0)]
        public long i; /* 当弱值且类型为 LuaObject 时，该字段用于缓存哈希值 */

        [FieldOffset(0)]
        public double n;
    }

    /* ---------------- Members ---------------- */

    private Primitive                _primitive;
    private LuaObject                _obj;
    private WeakReference<LuaObject> _weakObj;
    private LuaCFunction             _func; /* light C# function */
    private LuaType                  _type;
    internal readonly bool           weak; /* 仅限于 LuaTable 使用，LuaValue 内大部份接口都无时该字段，由 LuaTable 保证不泄漏弱值 */

    /* 不需要构造 None、nil、bool 的情况，直接以静态常量提供 */
    private LuaValue(LuaType type, bool weak = false) {
        _primitive = new Primitive();
        _obj       = null;
        _weakObj   = null;
        _func      = null;
        _type      = type;
        this.weak  = weak;
    }
    public LuaValue(IntPtr l, bool weak = false): this(new LuaType(LuaConst.TLIGHTUSERDATA), weak) {
        _primitive.l = l;
    }
    private LuaValue(bool b, bool weak = false): this(new LuaType(LuaConst.TBOOLEAN), weak) {
        _primitive.b = b;
    }
    public LuaValue(int i, bool weak = false): this((long)i, weak) { }
    public LuaValue(long i, bool weak = false): this(new LuaType(LuaConst.TNUMINT), weak) {
        _primitive.i = i;
    }
    public LuaValue(double n, bool weak = false): this(new LuaType(LuaConst.TNUMFLT), weak) {
        _primitive.n = n;
    }
    public LuaValue(LuaCFunction func, bool weak = false): this(new LuaType(LuaConst.TLCF), weak) {
        LuaDebug.AssertNotNull(func);
        _func = func;
    }
    public LuaValue(LuaObject obj, bool weak = false): this(obj.Type, weak) {
        LuaDebug.AssertNotNull(obj);
        _SetLuaObject(obj);
    }
    public LuaValue(string s, bool weak = false): this(new LuaString(s), weak) { }

    internal LuaValue _Clone(bool wantWeak = false) {
        LuaValue  clone = new LuaValue((LuaType)LuaConst.TNIL, wantWeak);
        LuaObject obj   = null;
        if (_type.IsLuaObject) {                /* 获取真正的 obj */
            if (weak) {                         /* 弱值引用直接在这里处理 */
                if (!_TryGetLuaObject(out obj)) /* 若弱值已失效，会返回 Nil */
                    return clone;
            } else
                obj = _obj;
        }
        clone._primitive = _primitive;
        clone._func      = _func;
        clone._type      = _type;
        if (obj != null)
            clone._SetLuaObject(obj); /* 设置弱值、类型、哈希值 */
        return clone;
    }

    /* [ Construct Helper ] */
    /* 以下函数只能用于构造新对象（如：构造器或 Clone），其他地方不能用！（因为无法保证调用者是引用而不是拷贝） */

    private void _SetLuaObject(LuaObject obj) {
        _ClearValue();
        if (weak) {
            if (_weakObj == null)
                _weakObj = new WeakReference<LuaObject>(obj);
            else
                _weakObj.SetTarget(obj);
            _primitive.i = obj.GetHashCode(); /* weakObj 每次都直接算哈希值 */
        } else {
            _obj = obj;
        }
        _type = obj.Type;
    }
    private void _ClearValue() {
        _obj     = null;
        _weakObj = null;
        _func    = null;
        _type    = LuaConst.TNONE;
    }

    /* ---------------- Properties ---------------- */

    public LuaType Type               => _type;
    public bool    IsNil              => _type.NotNoneTag == LuaConst.TNIL;
    public bool    IsBoolean          => _type.NotNoneVariant == LuaConst.TBOOLEAN;
    public bool    IsInt              => _type.NotNoneVariant == LuaConst.TNUMINT;
    public bool    IsFloat            => _type.NotNoneVariant == LuaConst.TNUMFLT;
    public bool    IsNumber           => _type.NotNoneTag == LuaConst.TNUMBER;
    public bool    IsString           => _type.NotNoneTag == LuaConst.TSTRING;
    public bool    IsTable            => _type.NotNoneTag == LuaConst.TTABLE;
    public bool    IsThread           => _type.NotNoneTag == LuaConst.TTHREAD;
    public bool    IsFunction         => _type.NotNoneTag == LuaConst.TFUNCTION;
    public bool    IsLuaObject        => _type.IsLuaObject;
    public bool    Valid              => _type.Raw != LuaConst.TNONE;
    public bool    Null               => IsNil || !Valid;
    public bool    CanConvertToString => IsNumber;
    public bool    CanConvertToNumber => IsString;

    /* ---------------- Type Conversion ---------------- */

    public bool ToBoolean() {
        LuaDebug.Check(Valid, "Can't convert invalid value to bool.");
        if (Null)
            return false;
        else if (IsBoolean)
            return Bool;
        else
            return true;
    }
    public bool ToNumber(out double result) {
        result = 0;
        if (IsFloat) {
            result = _primitive.n;
            return true;
        }
        if (IsInt) {
            result = _primitive.i;
            return true;
        } else if (CanConvertToNumber) {
            double d = LuaUtils.StringToDouble(Str, out int consumedCharCount);
            if (consumedCharCount == LObject<LuaString>().UTF8Length) {
                result = d;
                return true;
            }
        }
        return false;
    }
    public bool ToInteger(out long result) {
        return ToInteger(out result, LuaConfig.DEFAULT_TO_INT_MODE);
    }
    public bool ToInteger(out long result, ToIntMode mode) {
        result = 0;
        if (IsInt) {
            result = _primitive.i;
            return true;
        } else if (IsFloat) {
            double d = _primitive.n;
            double f = Math.Floor(d);
            if (d != f) { /* 非整型 */
                if (mode == ToIntMode.INT)
                    return false;
                else if (mode == ToIntMode.CEIL)
                    f += 1;
            }
            return LuaUtils.DoubleToLong(f, out result);
        } else if (CanConvertToNumber) {
            int consumedCharCount = LuaUtils.StringToLuaNumber(Str, out LuaValue luaNumber);
            if (consumedCharCount == LObject<LuaString>().UTF8Length)
                return luaNumber.ToInteger(out result, mode);
        }
        return false;
    }
    public bool ToString(out string result) {
        result = null;
        if (IsString) {
            result = Str;
            return true;
        }
        if (!CanConvertToString)
            return false;
        if (IsInt)
            result = _primitive.i.ToString();
        else if (IsFloat)
            result = _primitive.n.ToString();
        return true;
    }

    /* 这个 override 只是调试用途；从 Lua 层面来说，只有 string 或 number 能隐式转为字符串，用上面的 ToString(out string) 版本 */
    public override string ToString() {
        if (_type.Tag == LuaConst.TNONE)
            return "<none>";
        if (_type.IsLuaObject) {
            if (_TryGetLuaObject(out LuaObject obj))
                return obj.ToString();
            else
                return "<invalid-weak-object>";
        }
        switch (_type.NotNoneTag) {
        case LuaConst.TNIL:
            return "nil";
        case LuaConst.TBOOLEAN:
            return _primitive.b ? "true" : "false";
        case LuaConst.TLIGHTUSERDATA:
            return CommonUtils.ToString(_primitive.l);
        }
        switch (_type.NotNoneVariant) {
        case LuaConst.TNUMINT:
            return _primitive.i.ToString();
        case LuaConst.TNUMFLT:
            return _primitive.n.ToString();
        case LuaConst.TLCF:
            return $"<lcf {CommonUtils.ToHexStrintg(RuntimeHelpers.GetHashCode(_func))}>";
        }
        LuaDebug.Check(false, $"Unknown type: {_type}.");
        return "<unknown>";
    }

    /* ---------------- Value Methods ---------------- */

    /* [ Get/Change Value ] */

    public IntPtr LightUserData {
        get {
            LuaDebug.AssertTag(_type, LuaConst.TLIGHTUSERDATA);
            return _primitive.l;
        }
    }
    public bool Bool {
        get {
            LuaDebug.AssertTag(_type, LuaConst.TBOOLEAN);
            return _primitive.b;
        }
    }
    public long Int {
        get {
            LuaDebug.AssertVariant(_type, LuaConst.TNUMINT);
            return _primitive.i;
        }
    }
    public double Float {
        get {
            LuaDebug.AssertVariant(_type, LuaConst.TNUMFLT);
            return _primitive.n;
        }
    }
    public double Number {
        get {
            LuaDebug.AssertTag(_type, LuaConst.TNUMBER);
            if (IsFloat) {
                return _primitive.n;
            } else {
                return _primitive.i;
            }
        }
    }
    public LuaCFunction LightFunc {
        get {
            LuaDebug.AssertVariant(_type, LuaConst.TLCF);
            return _func;
        }
    }
    public LuaObject Object {
        get {
            LuaDebug.AssertLuaObject(_type);
            AssertNotWeak("get object");
            return _obj;
        }
    }
    public T  LObject<T>()
        where T : LuaObject {
        LuaDebug.AssertNotNull(_obj);
        AssertNotWeak("get object, use _TryGetLuaObject instead.");
        return (T)_obj;
    }
    public void LObject<T>(out T result)
        where   T : LuaObject {
        LuaDebug.AssertNotNull(_obj);
        AssertNotWeak("get object, use _TryGetLuaObject instead.");
        result = LObject<T>();
    }
    public string Str {
        get {
            LuaDebug.AssertTag(_type, LuaConst.TSTRING);
            return LObject<LuaString>().Str;
        }
    }

    internal bool CheckValidValue(out LuaObject obj) {
        /* 检查对象合法性，如果是弱对象且对象存在，则通过 obj 参数返回 */
        obj = null;
        if (_type.IsLuaObject) { /* 获取真正的 obj */
            if (weak) {
                return _TryGetLuaObject(out obj);
            } else {
                obj = _obj;
                return true;
            }
        }
        return Valid;
    }
    internal bool _TryGetLuaObject(out LuaObject obj) {
        obj = null;
        if (!_type.IsLuaObject)
            return false;
        if (!weak) {
            obj = _obj;
            return true;
        } else if (_weakObj == null) {
            return false;
        }
        return _weakObj.TryGetTarget(out obj);
    }

    internal bool _IsCompareByHash() {
        return _func == compareByHashMark;
    }
    internal void MarkCompareByHash() {
        _func = compareByHashMark;
    }

    /* ---------------- Value Convert ---------------- */

    internal static bool TryConvertToString<TPointer>(TPointer pointer)
        where            TPointer : IPointer {
        if (pointer.Value.ToString(out string str)) {
            pointer.Value = new LuaValue(str);
            return true;
        }
        return false;
    }
    internal static void ConvertToString<TPointer>(TPointer pointer)
        where            TPointer : IPointer {
        if (!TryConvertToString(pointer))
            throw new LuaRuntimeError("Can't convert to string.");
    }

    /* 调用前必须保证自己取到的是 struct 的“引用”（如：变量名、原生数组索引），像 List 返回的 struct 是拷贝，不能用以下接口 */
    /* 以下接口不能改变类系，如果要改变类型直接新建覆盖就行了，性能没区别 */
    internal void _RefChangeValue(IntPtr l) {
        LuaDebug.AssertTag(_type, LuaConst.TLIGHTUSERDATA);
        _primitive.l = l;
    }
    internal void _RefChangeValue(bool b) {
        LuaDebug.AssertTag(_type, LuaConst.TBOOLEAN);
        _primitive.b = b;
    }
    internal void _RefChangeValue(int i) {
        _RefChangeValue((long)i);
    }
    internal void _RefChangeValue(long i) {
        LuaDebug.AssertVariant(_type, LuaConst.TNUMINT);
        _primitive.i = i;
    }
    internal void _RefChangeValue(double n) {
        LuaDebug.AssertVariant(_type, LuaConst.TNUMFLT);
        _primitive.n = n;
    }
    internal void _RefChangeValue(LuaCFunction func) {
        LuaDebug.AssertVariant(_type, LuaConst.TLCF);
        LuaDebug.AssertNotNull(func);
        _func = func;
    }
    internal void _RefChangeValue(LuaObject obj) {
        LuaDebug.AssertNotNull(obj);
        _SetLuaObject(obj);
    }
    internal void _RefChangeValue(string s) {
        _RefChangeValue(new LuaString(s));
    }
    internal void _RefChangeNil() {
        _type = LuaConst.TNIL;
    }

    /* ---------------- Operation ---------------- */

    /* 这里的 Equal 是 rawequal，也就是 Lua 里 Table 使用的 equal。之所以在这实现是因为方便给 Dictionary 使用
       参考 ltable.c 里的全类型哈希实现（参考 luaH_newkey, mainposition）
       和 LuaObject 同理，这里实现的 Equals 仅用于字典，该 Equals 行为需要和 GetHashCode 对齐
      （即，Equals 返回 true 时，两个对象的 GetHashCode 返回值一定相等）
       如果需要 Lua 值的逻辑等价判断，使用 LuaState.Equal，该函数会在必要时读取元表 */
    public override int GetHashCode() {
        if (_type.IsLuaObject)
            return weak ? (int)_primitive.i : _obj.GetHashCode(); /* weakObj 的哈希值直接存在 _primitive 里 */
        switch (_type.Tag) {
        case LuaConst.TNONE:
        case LuaConst.TNIL:
            return 0;
        case LuaConst.TBOOLEAN:
            return _primitive.b.GetHashCode();
        case LuaConst.TLIGHTUSERDATA:
            return _primitive.l.GetHashCode();
        }
        switch (_type.NotNoneVariant) {
        case LuaConst.TNUMINT:
            return _primitive.i.GetHashCode();
        case LuaConst.TNUMFLT:
            if (ToInteger(out long i, ToIntMode.INT))
                return i.GetHashCode();
            else if (double.IsNaN(_primitive.n))
                throw new LuaRuntimeError("Can't hash NaN.");
            return _primitive.n.GetHashCode();
        case LuaConst.TLCF:
            return _func.GetHashCode();
        }
        throw new LuaCoreError("Unknown type.");
    }
    public bool Equals(LuaValue other) {
        return Equals(in other);
    }
    public bool Equals(in LuaValue other) {
        LuaDebug.Assert(!(weak && IsLuaObject) && !(other.weak && other.IsLuaObject), "Can't compare weak object");
        /* 检查类型是否相同 */
        if (_type.Raw != other._type.Raw) {                               /* 不是相同类型 */
            if (_type.NotNoneTag == other._type.NotNoneTag && IsNumber) { /* int 和 float 比较 */
                if (ToInteger(out long i1) && other.ToInteger(out long i2))
                    return i1 == i2;
            }
            return false;
        }

        if (_type.IsLuaObject) /* LuaObject 派生类可自定义实现，默认就是比较地址，字符串则是比较字符串 */
            return _obj.Equals(other._obj);

        switch (_type.Tag) {
        case LuaConst.TNONE:
            return false; /* 即使两个 none 比较也返回 false */
        case LuaConst.TNIL:
            return true;
        case LuaConst.TBOOLEAN:
            return _primitive.b == other._primitive.b;
        case LuaConst.TLIGHTUSERDATA:
            return _primitive.l == other._primitive.l;
        }

        switch (_type.NotNoneVariant) {
        case LuaConst.TNUMFLT:
            return _primitive.n == other._primitive.n;
        case LuaConst.TNUMINT:
            return _primitive.i == other._primitive.i;
        case LuaConst.TLCF:
            return _func.Equals(other._func);
        }
        LuaDebug.Check(false, $"Unknown type in rawequal, lhs: {_type}, rhs: {other._type}");
        return false;
    }
    public override bool Equals(object obj) {
        if (obj is LuaValue other)
            return Equals(other);
        return false;
    }

    /* 不应该使用 ==，这里声明是为了监听错误 */
    public static bool operator ==(LuaValue left, LuaValue right) {
        LuaDebug.Check(false, "Use Equals(in LuaValue) instead.");
        return left.Equals(right);
    }
    public static bool operator !=(LuaValue left, LuaValue right) {
        LuaDebug.Check(false, "Use Equals(in LuaValue) instead.");
        return !left.Equals(right);
    }

    /* 不考虑元表的情况，功能上是 LuaTable.Get */
    internal bool _RawIndex(in LuaValue key, out LuaValue result) {
        if (IsTable) {
            result = LObject<LuaTable>().Get(key);
            return !result.Null;
        }
        result = NIL;
        return false;
    }
    /// <warning>
    /// 不建议调用，该接口是给 LuaState.NewIndex 用的，主要是方便判断是否需要调用 _InvalidateFlags
    /// 如果需要 RawNewIndex 的功能，可以直接调用 LuaTable 的 Set，但要注意自己判断一下是否需要调用 _InvalidateFlags
    /// </warning>
    internal bool _RawNewIndex(in LuaValue key, in LuaValue value) {
        if (IsTable) {
            LObject(out LuaTable table);
            bool keyExists = !table.Get(key).IsNil;
            if (keyExists)
                table.Set(key, value);
            return keyExists; /* 仅当覆盖键值时，才返回 true，这种情况不需要调用 _InvalidateFlags */
        }
        return false;
    }

    /* ---------------- Utils ---------------- */

    [Conditional("DEBUG")]
    internal void AssertNotWeak(string usage) {
        LuaDebug.Assert(!weak, $"Weak value can't be used in: {usage}.");
    }
}

internal interface IPointer
{
    LuaValue Value { get; set; } /* 指针操作不会做任何安全性检查，如有需要使用 Valid（但也不保证一定安全） */
    bool     Valid { get; }      /* 判断容器是否还存在。**不用来**判断指针越界之类的 */
}

/* ================== Other ================== */

public partial class LuaState
{
    internal struct StackRawPtr : IPointer
    {
        private WeakReference<LuaState> _stateWRef;
        public readonly RawIdx          rawIdx;

        public StackRawPtr(LuaState state, RawIdx rawIdx) {
            _stateWRef  = new WeakReference<LuaState>(state);
            this.rawIdx = rawIdx;
        }

        public LuaValue Value {
            get {
                _stateWRef.TryGetTarget(out LuaState state);
                return state._stack[(int)rawIdx];
            }
            set {
                _stateWRef.TryGetTarget(out LuaState state);
                state._stack[(int)rawIdx] = value;
            }
        }
        public bool Valid => _stateWRef.TryGetTarget(out LuaState _);
    }

    internal LuaValue Index(LuaValue indexedTarget, in LuaValue key) {
        if (indexedTarget._RawIndex(key, out LuaValue tableValue)) {
            return tableValue;
        }

        /* luaV_finishget */
        for (int loop = 0; loop < LuaConfig.MAX_TAG_LOOP; loop++) {
            LuaValue tagMethod;
            if (indexedTarget.IsTable) { /* Table */
                tagMethod = FastGetTagMethod(indexedTarget.LObject<LuaTable>().Metatable, TagMethod.INDEX);
                if (tagMethod.Null) {
                    return LuaValue.NIL;
                }
            } else { /* Not table */
                tagMethod = GetTagMethod(indexedTarget, TagMethod.INDEX);
                if (tagMethod.Null)
                    throw new LuaRuntimeError($"Attempt to index a '{indexedTarget.Type}' value.");
            }

            if (tagMethod.IsFunction) /* __index 是个方法 */
                return CallTagMethodWithResult(tagMethod, indexedTarget, key);
            indexedTarget = tagMethod; /* __index 是个表 */
            if (indexedTarget._RawIndex(key, out tableValue))
                return tableValue;
        }
        throw new LuaRuntimeError("'__index' chain too long; possible loop.");
    }
    /* 如果是表，则返回对应的表上的新 value；否则返回 null */
    internal void NewIndex(LuaValue indexedTarget, in LuaValue key, in LuaValue value) {
        if (indexedTarget._RawNewIndex(key, value))
            return;

        for (int loop = 0; loop < LuaConfig.MAX_TAG_LOOP; loop++) {
            LuaValue tagMethod;
            if (indexedTarget.IsTable) { /* Table，新建键值的情况 */
                tagMethod = FastGetTagMethod(indexedTarget.LObject<LuaTable>().Metatable, TagMethod.NEWINDEX);
                if (tagMethod.Null) {
                    LuaTable table = indexedTarget.LObject<LuaTable>();
                    table.Set(key, value);
                    table._InvalidateFlags();
                    return;
                }
                /* else will try the metamethod */
            } else { /* Not table */
                tagMethod = GetTagMethod(indexedTarget, TagMethod.NEWINDEX);
                if (tagMethod.Null)
                    throw new LuaRuntimeError($"Attempt to newindex a '{indexedTarget.Type}' value.");
            }

            /* 尝试元方法 */
            if (tagMethod.IsFunction) {                                           /* __newindex 是个方法 */
                CallTagMethodWithoutResult(tagMethod, indexedTarget, key, value); /* __newindex 没有返回值 */
                return;
            }
            indexedTarget = tagMethod;
            if (indexedTarget._RawNewIndex(key, value))
                return;
        }
        throw new LuaRuntimeError("'__newindex' chain too long; possible loop.");
    }
}

}
