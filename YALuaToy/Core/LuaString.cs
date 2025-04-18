namespace YALuaToy.Core {

using System;
using System.Text;
using System.Runtime.InteropServices;
using YALuaToy.Const;

/* Todo - 如果用户读取字符串，则在非托管堆上维护一个字符串的拷贝，该拷贝要额外添加 '\0' 结尾 */
internal class CStringHandle
{
    public readonly UIntPtr size;
    public readonly IntPtr  cstringPtr;

    public CStringHandle(string str) {
        // cstringPtr = NativeMemory.AllocString(str);
    }
    ~CStringHandle() {
        // NativeMemory.FreeString(cstringPtr);
    }
}

/*
 * 注意！！！为了更方便的与 C# 层交互，字符串直接以 C# UTF-16 string 为基础实现。这意味着：
    - 想得到 utf8 字节码需要做一步转换，由 LuaString.GetUTF8 接口提供
    - 无法保留非法的 utf8 字节码！！！不会以二进制形式保留！！！体现在：非法十/十六进制字面值【会被转换】
      对于十进制字面值，大于 127 的情况都会被识别为无效字符（即，'\128'~'\255' 都会被解析为 '0xEFBFBD'，也就是 U+FFFD，无效字符）
      对于十六进制字面值，那就更随意了。随便写一串十六进制字面值，只要不符合 utf8 编码，那转到 string 里就是其他东西（充斥着 '0xEFBFBD'）
 * 由于 Lua 没有其他机制提供二进制字面值（比如 Python 有 b'...'，但“伟大”的 Lua 是没有这种东西的），
   所以没法提供【通用的二进制数据字面值】，有三种解决思路：
     1. 使用文件读取
     2. 把二进制数据写入 C# 层
     3. 用【Unicode 转义】作为二进制字面值（如：bin = '\u{0034}\u{FFFF}\u{34ef}'），然后在
        C# 侧封装一个 Userdata 把字符串转回【BigEndianUnicode】，此时得到的字节数组就是对应的
        32 位二进制字面值数据
 */
internal class LuaString : LuaObject
{
    /* 进行符合 Lua 标准的“字符”操作时，应先转为 utf-8 操作（包括扩展库代码），
       这取决于你写代码的目的，如果是编写符合 Lua 标准的操作时，通常要转为 utf8 bytes，然后把 byte “强制转换” char。这样做当然有风险
       注意，其实 Lua 官方也没有支持 utf8（把字符串当作字节流而已），这里选择 utf8 作为通用字节流格式而已。
       其实可以出个配置来指定统一格式，这里暂时懒得搞。因为在 C# safe 代码里貌似没办法直接读取 utf8 以外编码的字节流。
       具体操作可以参考 LuaUtils.StringToDouble/LuaUtils.StringToLong */
    private readonly string _str;
    private int             _utf8Length;
    private CStringHandle   _cstringHandle;

    /* 注：不要用 Str.Length 获取长度，行为与 Lua 不符。直接用 UTF8Length 接口即可 */
    public string Str => _str;
    public int    UTF8Length {
        get {
            if (_utf8Length == -1)
                _utf8Length = Encoding.UTF8.GetByteCount(_str);
            return _utf8Length;
        }
    }
    public bool Reserved => _type.NotNoneVariant == LuaConst.TRESERVEDSTR;

    public LuaString(string str, bool reserved = false) {
        if (reserved)
            _type = LuaConst.MarkLuaObject(LuaConst.TRESERVEDSTR);
        else
            _type = LuaConst.MarkLuaObject(LuaConst.TNORMALSTR);
        _str        = str;
        _utf8Length = -1;
    }

    protected override int HashCode() {
        return _str.GetHashCode();
    }
    public override bool Equals(LuaObject other) {
        if (other is LuaString str)
            return _str.Equals(str._str);
        return false;
    }
    public override string ToString() {
        return $"'{_str}'";
    }

    public byte[] GetUTF8() {
        return Encoding.UTF8.GetBytes(_str);
    }
    internal IntPtr GetCString() {
        if (_cstringHandle == null)
            _cstringHandle = new CStringHandle(_str);
        return _cstringHandle.cstringPtr;
    }
}

/* UserData 需要用 NativeMemory API 分配内存 */
/* LuaUserData 仅向 C 接口开放，C# 层没理由使用 UserData 吧，封装 LightUserData 就好了 （＾∀＾●）ﾉｼ */
internal class LuaUserData : LuaObject
{
    private int      _length;
    private LuaTable _metatable;

    public LuaTable Metatable {
        get => _metatable;
        set => _metatable = value;
    }
    public int Length => _length;

    public LuaUserData() {
        _type      = LuaConst.MarkLuaObject(LuaConst.TUSERDATA);
        _length    = 0;
        _metatable = null;
    }
    ~LuaUserData() {
        /* 回收内存 */
    }
}

}
