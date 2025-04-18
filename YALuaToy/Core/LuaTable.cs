namespace YALuaToy.Core {

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YALuaToy.Const;
using YALuaToy.Debug;

internal partial class LuaTable
    : LuaObject,
      IEnumerable<KeyValuePair<LuaValue, LuaValue>>
{
    private class EqualityComparer : IEqualityComparer<LuaValue>
    {
        bool IEqualityComparer<LuaValue>.Equals(LuaValue x, LuaValue y) {
            if (!x.CheckValidValue(out LuaObject xobj) || !y.CheckValidValue(out LuaObject yobj)) {
                /* 无效弱值在用户访问时应返回 false，在内部遍历时返回 true；无效弱值直接比较哈希值 */
                if (x._IsCompareByHash() || y._IsCompareByHash())
                    return x.GetHashCode() == y.GetHashCode();
                else
                    return false;
            }
            if (xobj != null || yobj != null) /* 对象比较 */
                return xobj != null && yobj != null && xobj.Equals(yobj);
            return x.Equals(in y);
        }
        int IEqualityComparer<LuaValue>.GetHashCode(LuaValue luaValue) {
            return luaValue.GetHashCode();
        }
    }

    private static readonly int              SWEEP_THRESHOLD  = LuaConfig.LUA_TABLE_SWEEP_THRESHOLD;
    private static readonly int              THRESHOLD_FACTOR = 3;
    private static readonly EqualityComparer equalityComparer = new EqualityComparer();

    /* ---------------- Members ---------------- */

    private bool                                          _hasWeak;
    private bool                                          _weakKey;
    private bool                                          _weakValue;
    private byte                                          _flags; /* 当前 table 作为元表时，特定元方法“不”存在的位标记，位对应 TagMethod */
    private int                                           _sweepCounter;
    private LuaTable                                      _metatable;
    private Dictionary<LuaValue, LuaValue>                _table;
    private IEnumerator<KeyValuePair<LuaValue, LuaValue>> _enumeratorCache; /* 用于 LuaState.Next 接口 */

    public LuaTable() {
        _type    = LuaConst.MarkLuaObject(LuaConst.TTABLE);
        _weakKey = _weakValue = false;
        _hasWeak              = false; /* 当前是否存在 weak 元素 */
        _flags                = 0xFF;  /* 初始化为全 1 */
        _sweepCounter         = 0;
        _metatable            = null;
        _table                = null;
        _enumeratorCache      = null;
    }

    /* ---------------- Properties ---------------- */

    public bool IsDummy => _table == null;
    public bool WeakKey {
        get => _weakKey;
        set { _weakKey = value; }
    }
    public bool WeakValue {
        get => _weakValue;
        set { _weakValue = value; }
    }
    public LuaTable Metatable {
        get => _metatable;
        set {
            _metatable = value;
            if (_metatable != null) { /* 设置弱表信息 */
                LuaValue modeValue = _metatable.Get(new LuaValue("__mode"));
                if (!modeValue.Null) {
                    LuaDebug.AssertTag(modeValue, LuaConst.TSTRING);
                    string mode = modeValue.Str;
                    WeakKey     = mode.Contains('k');
                    WeakValue   = mode.Contains('v');
                }
            }
        }
    }
    public byte Flags => _flags;

    /* ---------------- Dictionary Interface ---------------- */

    /* 如果 key 类型非法则会抛出错误，key 合法的情况下一定返回（最差也是 nil）；如果是弱键值，要新建一个 LuaValue 返回 */
    public LuaValue Get(in LuaValue key) { /* 兼顾 ContainsKey 的功能 */
        if (IsDummy)
            return LuaValue.NIL;
        if (key.IsNil)
            return LuaValue.NIL;
        _AddSweepCount(1);

        if (key.IsFloat)
            if (key.ToInteger(out long i, ToIntMode.INT))
                return _DoGet(new LuaValue(i));
        return _DoGet(key);
    }
    private LuaValue _DoGet(in LuaValue key) {
        LuaDebug.AssertNotNone(key.Type);
        if (_table.TryGetValue(key, out LuaValue value)) {
            if (value.Null)
                return LuaValue.NIL;
            if (!value.weak) {
                return value;
            } else if (value.CheckValidValue(out LuaObject obj)) {
                LuaDebug.Check(WeakValue);
                if (obj != null)
                    return new LuaValue(obj);
                return value._Clone();
            }
        }
        return LuaValue.NIL;
    }
    /* 注意！！对表调用 Set 后，如果 key 是字符串，则还要检查一下是否要调用 _InvalidateFlags */
    public void Set(LuaValue key, in LuaValue value) {
        /* 弱键值这一特性**仅用于** LuaTable，其他地方是无感的，LuaTable 的 API 也不能暴露这点 */
        LuaDebug.AssertNotNone(key.Type);
        LuaDebug.AssertNotNone(value.Type);
        if (key.IsNil)
            throw new LuaRuntimeError("Table index is nil.");
        if (IsDummy)
            _table = new Dictionary<LuaValue, LuaValue>(equalityComparer);

        if (key.IsFloat)
            if (key.ToInteger(out long i, ToIntMode.INT))
                key = new LuaValue(i);

        if (value.IsNil) {
            _table.Remove(key);
        } else if (WeakKey) {
            _table[key._Clone(true)] = WeakValue ? value._Clone(true) : value;
        } else {
            _table[key] = WeakValue ? value._Clone(true) : value;
        }
        if (!_hasWeak)
            _hasWeak = WeakKey || WeakValue;
        _enumeratorCache = null;
        _AddSweepCount(2);
    }

    /* 保证先遍历数组部分 */
    public IEnumerator<KeyValuePair<LuaValue, LuaValue>> GetEnumerator() {
        if (IsDummy)
            yield break;
        int      arrayLength = 0;
        LuaValue key         = new LuaValue(1);
        LuaValue value       = _DoGet(key);
        while (!value.IsNil) {
            yield return new KeyValuePair<LuaValue, LuaValue>(key, value);
            arrayLength++;
            key._RefChangeValue(arrayLength + 1);
            value = _DoGet(key);
        }
        foreach (var pair in _table) {
            if (pair.Key.Null || pair.Value.Null)
                continue;
            if (!pair.Key.CheckValidValue(out _) || !pair.Value.CheckValidValue(out _))
                continue;
            if (pair.Key.IsInt && pair.Key.Int > 0 && pair.Key.Int <= arrayLength)
                continue;
            yield return new KeyValuePair<LuaValue, LuaValue>(
                pair.Key.weak ? pair.Key._Clone() : pair.Key, pair.Value.weak ? pair.Value._Clone() : pair.Value
            );
        }
    }
    IEnumerator IEnumerable.GetEnumerator() { /* ICollection 继承了非泛型版的 IEnumerable */
        return GetEnumerator();
    }

    public int GetArrayLength() {
        if (IsDummy)
            return 0;
        int      length = 0;
        LuaValue key    = new LuaValue(1);
        while (!_DoGet(key).IsNil) {
            length++;
            key._RefChangeValue(length + 1);
        }
        return length;
    }

    /* ---------------- Utils ---------------- */

    public override string ToString() {
        HashSet<object> visited = new HashSet<object>();
        return ToString(visited);
    }
    public string ToString(HashSet<object> visited) {
        int           i       = 1;
        StringBuilder sb      = new StringBuilder();
        bool          hasElem = false;

        string ValueToString(in LuaValue value) {
            if (!value.IsTable)
                return value.ToString();
            if (!value._TryGetLuaObject(out LuaObject obj) || visited.Contains(obj))
                return $"<tb {CommonUtils.ToHexStrintg(RuntimeHelpers.GetHashCode(this))}>";
            visited.Add(obj);
            return ((LuaTable)obj).ToString(visited);
        }

        sb.Append('{');
        foreach (var pair in this) { /* 要先遍历数组 */
            if (pair.Key.IsInt && pair.Key.Int == i) {
                sb.Append(ValueToString(pair.Value));
                sb.Append(", ");
                i++;
            } else {
                sb.Append($"{ValueToString(pair.Key)}:{ValueToString(pair.Value)}, ");
            }
            hasElem = true;
        }
        if (hasElem)
            sb.Remove(sb.Length - 2, 2); /* 移除尾部逗号 */
        sb.Append('}');
        // return $"<{CommonUtils.ToHexStrintg(RuntimeHelpers.GetHashCode(this))}>{sb}";
        return Regex.Replace(sb.ToString(), @"[\n\t]", " ");
    }

    private void _AddSweepCount(int addCount = 1) {
        if (IsDummy || !_hasWeak)
            return;
        _sweepCounter += addCount;
        if (_sweepCounter <= (THRESHOLD_FACTOR * Math.Max(SWEEP_THRESHOLD, _table.Count)))
            return;
        _Sweep();
    }

    internal int _Sweep() { /* 暴露给 internal 是仅供测试使用 */
        List<LuaValue> keysToRemove = new List<LuaValue>();
        foreach (var pair in _table) {
            if (pair.Key.weak && !pair.Key.CheckValidValue(out _))
                keysToRemove.Add(pair.Key);
            if (pair.Value.weak && !pair.Value.CheckValidValue(out _))
                keysToRemove.Add(pair.Key);
        }
        foreach (var key in keysToRemove) {
            key.MarkCompareByHash();
            _table.Remove(key);
        }
        _sweepCounter = 0;
        _hasWeak      = false;
        return keysToRemove.Count;
    }

    internal bool GetEnumeratorNext(in LuaValue currkey, out IEnumerator<KeyValuePair<LuaValue, LuaValue>> result) {
        /* 具体看 LuaState.Next 的接口描述 */
        if (_enumeratorCache != null && _enumeratorCache.Current.Key.Equals(in currkey)) { /* 有效 cache */
            if (_enumeratorCache.MoveNext()) {
                result = _enumeratorCache;
                return true;
            }
        } else { /* 无效 cache，重新获取 */
            _enumeratorCache = GetEnumerator();
            while (_enumeratorCache.MoveNext()) {
                if (currkey.IsNil) {
                    result = _enumeratorCache;
                    return true;
                }
                if (!_enumeratorCache.Current.Key.Equals(in currkey))
                    continue;
                if (_enumeratorCache.MoveNext()) {
                    result = _enumeratorCache;
                    return true;
                }
                break;
            }
        }
        /* 跑到头了 或 获取失败 */
        _enumeratorCache = result = null;
        if (currkey.IsNil || !Get(currkey).IsNil) /* 如果键为 nil 还获取失败，表示该表为空表；另外，如果有键，则表示跑到头了 */
            return false;
        else /* 获取失败的情况 */
            throw new LuaRuntimeError($"invalid key to 'next': {currkey}");
    }
}

/* 上面的 LuaTable 负责实现字典的功能，其他接口和数据结构放这里，免得影响代码阅读 */
internal partial class LuaTable
{
    internal void _InvalidateFlags() {
        _flags = 0; /* 重置元方法标记，这样就会查找元方法了 */
    }
    public bool GetFlags(TagMethod tag) {
        LuaDebug.AssertCanCacheTagMethod(tag);
        return (_flags & (1 << (int)tag)) != 0;
    }
    private void _SetFlags(TagMethod tag, bool setTag = true) {
        LuaDebug.AssertCanCacheTagMethod(tag);
        if (setTag)
            _flags |= (byte)(1 << (int)tag);
        else
            _flags &= (byte) ~(1 << (int)tag);
    }
}

}
