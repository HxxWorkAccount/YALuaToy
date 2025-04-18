namespace YALuaToy.Compilation {

using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Core;
using YALuaToy.Compilation.Antlr;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal static class LuaLexerUtils
{
    public static string ReadShortString(string raw, int type) {
        LuaDebug.Assert(IsShortStringToken(type), "Invalid short string.");
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < raw.Length; i++) {
            LexerCheck(raw[0] == raw[raw.Length - 1], "Invalid string: not same delimiter.", type);
            if (i == 0 || i == raw.Length - 1)
                continue; /* 跳过首尾的分隔符 */
            char c = raw[i];
            switch (c) {
            case '\n':
            case '\r':
                throw new LuaSyntaxError("Unfinished short string.", type);
            case '\\': {      /* unescape */
                c = raw[++i]; /* 跳过转义前缀 */
                switch (c) {
                case 'a':
                    c = '\a';
                    break;
                case 'b':
                    c = '\b';
                    break;
                case 'f':
                    c = '\f';
                    break;
                case 'n':
                    c = '\n';
                    break;
                case 'r':
                    c = '\r';
                    break;
                case 't':
                    c = '\t';
                    break;
                case 'v':
                    c = '\v';
                    break;
                case 'x': { /* 接下来要连续识别两个十六进制数 */
                    sb.Append(ReadHexEsc(raw, ref i));
                    goto nosave;
                }
                case 'u':
                    sb.Append(ReadUnicodeEsc(raw, ref i));
                    goto nosave;
                case '\n':
                case '\r':
                    c = ReadNewLine(raw, ref i);
                    break;
                case '\\':
                case '\"':
                case '\'':
                    break;
                case 'z': {
                    SkipWhiteSpace(raw, ref i);
                    goto nosave;
                }
                default: { /* 只剩下一种情况，就是十进制转义（如：'\123'） */
                    sb.Append(ReadDecimalEsc(raw, ref i));
                    goto nosave;
                }
                }
                sb.Append(c);
            nosave:
                break;
            }
            default:
                sb.Append(c);
                break;
            }
        }
        return sb.ToString();
    }
    public static string ReadLongString(string raw) {
        SkipLongStringSeperator(raw, out int start, out int end); /* 先跳过首尾分隔符 */
        StringBuilder sb = new StringBuilder();
        int           i  = start;
        if (IsNewLine(raw[i])) { /* 跳过第一个换行 */
            ReadNewLine(raw, ref i);
            i++;
        }
        for (; i <= end; i++) {
            char c = raw[i];
            if (c == '\n' || c == '\r')
                c = ReadNewLine(raw, ref i);
            if (c == ']' && (end - i >= start)) {
                /* 这里要做下安全检查，看看字符串内是否有相同闭合分隔符 */
                LexerCheck(!MatchLongStringSeperator(raw, start, i), $"Invalid long string: {raw}. at {i}");
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /* 注意，下面这批接口总是假设：“下次循环开头还会进行一次 i++”，所以调用后 i 的位置处于【读取目标的最后一个字符上】 */
    private static char ReadNewLine(string raw, ref int i) {
        LuaDebug.Assert(IsNewLine(raw[i]), "Invalid new line.");
        if (i + 1 < raw.Length && IsNewLine(raw[i + 1]) && raw[i] != raw[i + 1])
            i++;
        return '\n';
    }
    internal static string ReadHexEsc(string raw, ref int i) {
        /* 这里要批量读取，然后按 utf-8 解码 */
        LuaDebug.Assert(raw[i] == 'x');
        List<byte> bytes = new List<byte>();
        while (true) {
            char c      = raw[++i];
            int  twohex = 0;
            LexerCheck(LuaUtils.TryCharToInt(c, true, out twohex));
            c = raw[++i];
            LexerCheck(LuaUtils.TryCharToInt(c, true, out int hex));
            twohex = (twohex << 4) + hex;
            bytes.Add((byte)twohex);
            if (i + 2 < raw.Length && raw[i + 1] == '\\' && raw[i + 2] == 'x') {
                i += 2;
                continue;
            }
            break;
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    internal static string ReadUnicodeEsc(string raw, ref int i) {
        /* 注意，这里会把 unicode 转为 C# 的 UTF16 存储 */
        LuaDebug.Assert(raw[i] == 'u');
        i++;
        LexerCheck(raw[i] == '{');
        i++;
        int start = i;
        while (i < raw.Length && raw[i] != '}')
            i++;
        LexerCheck(i < raw.Length, $"Invalid escape format: {raw.Substring(start)}");
        string hex = raw.Substring(start, i - start);
        LexerCheck(int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int unicode), $"Invalid unicode: {hex}");
        return char.ConvertFromUtf32(unicode);
    }
    internal static string ReadDecimalEsc(string raw, ref int i) {
        LexerCheck(char.IsDigit(raw[i]));
        int start = i;
        while (i < raw.Length && i < start + 3 && char.IsDigit(raw[i]))
            i++;
        string decstr = raw.Substring(start, i - start);
        LexerCheck(int.TryParse(decstr, out int dec), $"Invalid decimal escape: {decstr}");
        LexerCheck(dec <= 255, $"Decimal escape out of range: {dec}"); /* C# 的 UTF16 字符可以支持更大范围，不过还是按 Lua 标准来写 */
        if (dec > 127)
            Debug.WriteLine($"Warning: Decimal escape out of ASCII range: {dec}");
        i--;
        return Encoding.UTF8.GetString(new byte[] { (byte)dec });
    }
    private static void SkipWhiteSpace(string raw, ref int i) {
        LuaDebug.Assert(raw[i] == 'z');
        i++;
        while (i < raw.Length && char.IsWhiteSpace(raw[i]))
            i++;
        i--;
    }
    private static void SkipLongStringSeperator(string raw, out int start, out int end) {
        LuaDebug.Assert(raw[0] == '[' && raw[raw.Length - 1] == ']');
        start = 1;
        end   = raw.Length - 2;
        for (; start < end; start++, end--) {
            if (raw[start] == '[') {
                LexerCheck(raw[end] == ']');
                start++;
                end--;
                return;
            }
            LexerCheck(raw[start] == '=' && raw[end] == '=');
        }
        LexerCheck(false, $"Invalid seperator of long string: {raw}");
    }

    private static bool MatchLongStringSeperator(string raw, int start, int i) {
        if (i + start > raw.Length || raw[i] != ']' || raw[i + start - 1] != ']')
            return false;
        for (int j = 1; j < start - 1; j++) {
            if (raw[i + j] == '=')
                continue;
            return false;
        }
        return true;
    }
    private static bool IsNewLine(char c) {
        return c == '\n' || c == '\r';
    }

    public static void LexerCheck(bool condition, string message = "") {
        if (!condition)
            throw new LuaSyntaxError(message);
    }
    public static void LexerCheck(bool condition, string message, int type) {
        if (!condition)
            throw new LuaSyntaxError(message, type);
    }

    public static string GetTypeName(int type) {
        switch (type) {
        case LuaLexer.Eof:
            return "<eof>";
        case LuaLexer.FLOAT:
        case LuaLexer.HEX_FLOAT:
            return "<number>";
        case LuaLexer.INT:
        case LuaLexer.HEX:
            return "<integer>";
        case LuaLexer.NAME:
            return "<name>";
        case LuaLexer.CHARSTRING:
        case LuaLexer.NORMALSTRING:
        case LuaLexer.LONGSTRING:
            return "<string>";
        }
        if (type > LuaLexer.DBCOLON)
            return "<noname>";
        string literal = LuaLexer.DefaultVocabulary.GetLiteralName(type);
        return literal.Substring(1, literal.Length - 2); /* 去除首尾的分隔符 */
    }

    public static bool IsIntToken(int type) {
        return type == LuaLexer.INT || type == LuaLexer.HEX;
    }
    public static bool IsFloatToken(int type) {
        return type == LuaLexer.FLOAT || type == LuaLexer.HEX_FLOAT;
    }
    public static bool IsNumberToken(int type) {
        return IsIntToken(type) || IsFloatToken(type);
    }
    public static bool IsShortStringToken(int type) {
        return IsStringToken(type) && type != LuaLexer.LONGSTRING;
    }
    public static bool IsLongStringToken(int type) {
        return type == LuaLexer.LONGSTRING;
    }
    public static bool IsStringToken(int type) {
        return type == LuaLexer.NORMALSTRING || type == LuaLexer.CHARSTRING || type == LuaLexer.LONGSTRING;
    }
}

}
