namespace YALuaToy.Core {

using System;
using System.Collections.Generic;
using System.Text;
using YALuaToy.Const;
using YALuaToy.Debug;

internal static class LuaUtils
{
    public static double StringToDouble(string s, out int consumedCharCount, bool toEnd = true) {
        return StringToDouble(s, 0, out consumedCharCount, toEnd);
    }
    public static double StringToDouble(string s, int start, out int consumedCharCount, bool toEnd = true) {
        /* 简易模仿 C99 规范的 strtod，在精度舍入和边界处理上有差异 */
        const int MAXSIGDIG = 30;
        consumedCharCount   = 0;
        int    i            = start;
        int    e            = 0;
        int    sigdig       = 0;
        int    nosigdig     = 0;
        double result       = 0;
        int    realBase     = 10;
        bool   hasDot       = false;
        bool   hex          = false;
        byte[] chars        = Encoding.UTF8.GetBytes(s); /* 与 Lua 标准一致 */

        bool ConsumeSingleWhiteSpace() {
            if (i < chars.Length && char.IsWhiteSpace((char)chars[i])) {
                i++;
                return true;
            }
            return false;
        }
        int ConsumeSign() {
            if (chars[i] == '-') {
                i++;
                return -1;
            } else if (s[i] == '+') {
                i++;
            }
            return 1;
        }

        while (ConsumeSingleWhiteSpace()) { } /* 跳过开头空白 */
        if (i >= chars.Length)
            return 0;
        int sign = ConsumeSign();

        /* 判断是否为十六进制 */
        if (i + 1 < chars.Length && chars[i] == '0' && (chars[i + 1] == 'x' || chars[i + 1] == 'X')) {
            i += 2;
            realBase = 16;
            hex      = true;
        }

        /* 底数部分 */
        for (; i < chars.Length; i++) {
            if (chars[i] == '.') {
                if (hasDot)
                    break;
                hasDot = true;
            } else {
                int digit = CharToInt((char)chars[i], hex);
                if (digit == -1)
                    break;
                else if (sigdig == 0 && digit == 0) /* 无效 0 */
                    nosigdig++;
                else if (++sigdig <= MAXSIGDIG)
                    result = result * realBase + digit;
                else
                    e++; /* 过多的数字，但仍会增加指数 */
                if (hasDot)
                    e--;
            }
        }
        if (nosigdig + sigdig == 0)
            return 0;
        consumedCharCount = i - start;
        if (hex)
            e *= 4;

        /* 指数部分 */
        if (i + 1 < chars.Length && ((!hex && (chars[i] == 'e' || chars[i] == 'E')) || (hex && (chars[i] == 'p' || chars[i] == 'P')))) {
            i++;
            int expSign = ConsumeSign();
            int exp     = 0;
            if (i >= chars.Length || CharToInt((char)chars[i], false) == -1) {
                consumedCharCount = 0;
                return 0;
            }
            for (; i < chars.Length; i++) {
                int digit = CharToInt((char)chars[i], false); /* 指数部分一定是十进制表示 */
                if (digit == -1)
                    break;
                exp = exp * 10 + digit;
            }
            e += expSign * exp;
            consumedCharCount = i - start;
        }

        /* 消耗剩余空格 l_str2dloc */
        if (toEnd) {
            while (ConsumeSingleWhiteSpace()) { }
            if (i < chars.Length) { /* 如果没消耗完，那就当作识别失败 */
                consumedCharCount = 0;
                return 0;
            }
            consumedCharCount = i - start;
        }
        result *= sign;
        double p = Math.Pow(hex ? 2 : 10, e);
        /* 低精度范围内用 decimal 提高准确性（防止 `53/10 != 5.3` 之类的代码。。。折中处理） */
        if (Math.Abs(result) > 0.001 && Math.Abs(result) < 1000000 && p > 0.001 && p < 1000000)
            return (double)((decimal)result * (decimal)p);
        return result * p;
    }
    public static long StringToLong(string s, out int consumedCharCount) {
        return StringToLong(s, 0, out consumedCharCount);
    }
    public static long StringToLong(string s, int start, out int consumedCharCount) {
        const ulong MAX_INTEGER = long.MaxValue;
        const ulong MAXBY10     = MAX_INTEGER / 10;
        const ulong MAXLASTD    = MAX_INTEGER % 10;
        consumedCharCount       = 0;
        ulong result            = 0;
        int   i                 = start;
        bool  empty             = true;
        byte[] chars            = Encoding.UTF8.GetBytes(s); /* 与 Lua 标准一致 */

        bool ConsumeSingleWhiteSpace() {
            if (i < chars.Length && char.IsWhiteSpace((char)chars[i])) {
                i++;
                return true;
            }
            return false;
        }
        int ConsumeSign() {
            if (chars[i] == '-') {
                i++;
                return -1;
            }
            return 1;
        }

        while (ConsumeSingleWhiteSpace()) { } /* 跳过开头空白 */
        if (i >= chars.Length)
            return 0;
        int sign = ConsumeSign();
        if (i + 1 < chars.Length && chars[i] == '0' && (chars[i + 1] == 'x' || chars[i + 1] == 'X')) { /* 16 进制 */
            i += 2;
            for (; i < chars.Length; i++) {
                int digit = CharToInt((char)chars[i], true);
                if (digit == -1)
                    break;
                result = result * 16 + (ulong)digit;
                empty  = false;
            }
        } else { /* 10 进制 */
            for (; i < chars.Length; i++) {
                int digit = CharToInt((char)chars[i], false);
                if (digit == -1)
                    break;
                if (result >= MAXBY10 && (result > MAXBY10 || (ulong)digit > MAXLASTD)) /* 读取十进制会做溢出检测 */
                    return 0;
                result = result * 10 + (ulong)digit;
                empty  = false;
            }
        }
        while (ConsumeSingleWhiteSpace()) { } /* 跳过结尾空白 */
        if (empty || i < chars.Length)        /* 识别失败 */
            return 0;
        consumedCharCount = i - start;
        return sign == -1 ? -(long)result : (long)result;
    }

    public static int StringToLuaNumber(string s, out LuaValue result) {
        int  consumedCharCount;
        long i = StringToLong(s, out consumedCharCount);
        if (consumedCharCount > 0) {
            result = new LuaValue(i);
            return consumedCharCount;
        }
        double d = StringToDouble(s, out consumedCharCount);
        if (consumedCharCount > 0) {
            result = new LuaValue(d);
            return consumedCharCount;
        }
        result = LuaValue.NIL;
        return 0;
    }

    public static bool DoubleToLong(double d, out long result) {
        if (d >= long.MinValue && d <= long.MaxValue) {
            result = (long)d;
            return true;
        }
        result = 0;
        return false;
    }

    public static bool ConvertToDecimal(double d, out decimal result) {
        if (double.IsNaN(d) || double.IsInfinity(d) || d > (double)decimal.MaxValue || d < (double)decimal.MinValue) {
            result = 0;
            return false;
        } else {
            result = (decimal)d;
            return true;
        }
    }

    /* 十进制/十六进制字符 -> 数字 */
    public static int CharToInt(char c, bool hex = false) {
        if ('0' <= c && c <= '9')
            return c - '0';
        if (hex && 'a' <= c && c <= 'f')
            return c - 'a' + 10;
        if (hex && 'A' <= c && c <= 'F')
            return c - 'A' + 10;
        return -1;
    }
    public static bool TryCharToInt(char c, bool hex, out int i) {
        i = CharToInt(c, hex);
        return i != -1;
    }

    /* 格式化字符串，最多提供五个参数，若不够可以重复调用 FormatString。不使用 params 是为了避免装箱 */
    private static void _FormatString<T>(StringBuilder sb, string fmt, ref int pos, T arg) {
        int i = fmt.IndexOf('%', pos);
        if (i == -1) {
            sb.Append(fmt.Substring(pos)); /* 从 pos 到最后 */
            throw new LuaRuntimeError("Invalid format: '%' not found.");
        }
        sb.Append(fmt.Substring(pos, i - pos)); /* 从 pos 到 '%' 前 */
        if (i + 1 >= fmt.Length)
            throw new LuaRuntimeError("Invalid format: '%' at the end of string.");

        char c = fmt[i + 1];
        switch (c) {
        case 's': /* 字符串 */
            sb.Append(arg == null ? "(null)" : arg.ToString());
            break;
        case 'c': /* 整型字符（CLua 技巧，用整型表示字符） */
            int  val = Convert.ToInt32(arg);
            char ch  = (char)val;
            if (!char.IsControl(ch))
                sb.Append(ch);
            else
                sb.AppendFormat("<\\{0}>", val);
            break;
        case 'd': /* int */
            sb.Append(Convert.ToInt32(arg));
            break;
        case 'I': /* long */
            sb.Append(Convert.ToInt64(arg));
            break;
        case 'f': /* double */
            sb.Append(Convert.ToDouble(arg));
            break;
        case 'p': /* 指针 */
            object pArg = arg;
            sb.Append($"0x{pArg.GetHashCode():X}");
            break;
        case 'U': /* int 转为 utf8 字符 */
            int code = Convert.ToInt32(arg);
            sb.Append(Char.ConvertFromUtf32(code));
            break;
        case '%': /* 转义 % */
            sb.Append("%");
            break;
        default:
            throw new LuaRuntimeError($"Invalid format: '%{c}' in '{fmt}'.");
        }
        pos = i + 2;
    }
    public static string FormatString<T1>(string fmt, T1 arg1) {
        int           pos = 0;
        StringBuilder sb  = new StringBuilder();
        _FormatString(sb, fmt, ref pos, arg1);
        return sb.ToString();
    }
    public static string FormatString<T1, T2>(string fmt, T1 arg1, T2 arg2) {
        int           pos = 0;
        StringBuilder sb  = new StringBuilder();
        _FormatString(sb, fmt, ref pos, arg1);
        _FormatString(sb, fmt, ref pos, arg2);
        return sb.ToString();
    }
    public static string FormatString<T1, T2, T3>(string fmt, T1 arg1, T2 arg2, T3 arg3) {
        int           pos = 0;
        StringBuilder sb  = new StringBuilder();
        _FormatString(sb, fmt, ref pos, arg1);
        _FormatString(sb, fmt, ref pos, arg2);
        _FormatString(sb, fmt, ref pos, arg3);
        return sb.ToString();
    }
    public static string FormatString<T1, T2, T3, T4>(string fmt, T1 arg1, T2 arg2, T3 arg3, T4 arg4) {
        int           pos = 0;
        StringBuilder sb  = new StringBuilder();
        _FormatString(sb, fmt, ref pos, arg1);
        _FormatString(sb, fmt, ref pos, arg2);
        _FormatString(sb, fmt, ref pos, arg3);
        _FormatString(sb, fmt, ref pos, arg4);
        return sb.ToString();
    }
    public static string FormatString<T1, T2, T3, T4, T5>(string fmt, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) {
        int           pos = 0;
        StringBuilder sb  = new StringBuilder();
        _FormatString(sb, fmt, ref pos, arg1);
        _FormatString(sb, fmt, ref pos, arg2);
        _FormatString(sb, fmt, ref pos, arg3);
        _FormatString(sb, fmt, ref pos, arg4);
        _FormatString(sb, fmt, ref pos, arg5);
        return sb.ToString();
    }

    public static uint Mask1(int n, int p) => (~((~0u) << n)) << p; /* 从 p 位置向高位创建 n 个连续 1，其他是 0 */
    public static uint Mask0(int n, int p) => ~Mask1(n, p);         /* 从 p 位置向高位创建 n 个连续 0，其他是 1 */

    public static bool NumberLessThan(in LuaValue num1, in LuaValue num2) {
        LuaDebug.AssertTag(num1.Type, LuaConst.TNUMBER);
        LuaDebug.AssertTag(num2.Type, LuaConst.TNUMBER);
        if (num1.Type.NotNoneVariant == num2.Type.NotNoneVariant) {
            if (num1.IsInt)
                return num1.Int < num2.Int;
            else
                return num1.Float < num2.Float;
        }
        if (num1.IsInt)
            return num1.Int < num2.Float;
        else
            return !(num2.Int <= num1.Float);
    }
    public static bool NumberLessEqual(in LuaValue num1, in LuaValue num2) {
        LuaDebug.AssertTag(num1.Type, LuaConst.TNUMBER);
        LuaDebug.AssertTag(num2.Type, LuaConst.TNUMBER);
        if (num1.Type.NotNoneVariant == num2.Type.NotNoneVariant) {
            if (num1.IsInt)
                return num1.Int <= num2.Int;
            else
                return num1.Float <= num2.Float;
        }
        if (num1.IsInt)
            return num1.Int <= num2.Float;
        else
            return !(num2.Int < num1.Float);
    }

    public static bool IsErrorStatus(ThreadStatus threadStatus) {
        return threadStatus > ThreadStatus.YIELD;
    }

    public static bool IsValidErrorFunc(RawIdx rawIdx) {
        return rawIdx > RawIdx.InvalidErrorFunc;
    }
}

public static class CommonUtils
{
    public static void Shrink<T>(List<T> list, int remained) {
        LuaDebug.Check(remained <= list.Count);
        remained = Math.Clamp(remained, 0, list.Count);
        list.RemoveRange(remained, list.Count - remained);
    }
    public static void RemoveLastElems<T>(List<T> list, int count) {
        LuaDebug.Check(count <= list.Count);
        count = Math.Clamp(count, 0, list.Count);
        list.RemoveRange(list.Count - count, count);
    }
    public static T[] GetLast<T>(List<T> list, int count) {
        int start   = list.Count >= 40 ? list.Count - count : 0;
        var subList = list.GetRange(start, list.Count - start);
        return subList.ToArray();
    }

    public static string ToHexStrintg(long i) {
        return $"0x{i:X}";
    }
    public static string ToString(IntPtr p) {
        return $"<{ToHexStrintg(p)}>";
    }
}

/* ================== Lua Operation ================== */

internal enum RawOperation {
    ADD,
    SUB,
    MUL,
    BAND,
    BOR,
    BXOR,
    SHL,
    SHR,
}

internal static class LuaOperation
{
    public static long Div(long lhs, long rhs) { /* floor(lhs/rhs) */
        if (rhs == 0)
            throw new LuaRuntimeError("Attempt to divide by zero.");
        if (rhs == -1)
            return 0 - lhs;
        long q = lhs / rhs;
        if ((lhs ^ rhs) < 0 && lhs % rhs != 0) /* 负数取整，向下取整 */
            q -= 1;
        return q;
    }
    public static long Mod(long lhs, long rhs) {
        /* a - (a / b) * b，除法要向下取整 */
        return lhs - Div(lhs, rhs) * rhs;
    }
    public static long LeftShift(long lhs, long rhs) {
        const int BITS = 64;
        if (rhs < 0) {
            if (rhs <= -BITS)
                return 0;
            else
                return (long)((ulong)lhs >> (int)-rhs); /* Lua 使用逻辑右移 */
        } else {
            if (rhs >= BITS)
                return 0;
            else
                return (long)((ulong)lhs << (int)rhs);
        }
    }
    public static long RawOperate(RawOperation op, long lhs, long rhs) { /* 位移用 LeftShift */
        switch (op) {
        case RawOperation.ADD:
            return (long)((ulong)lhs + (ulong)rhs);
        case RawOperation.SUB:
            return (long)((ulong)lhs - (ulong)rhs);
        case RawOperation.MUL:
            return (long)((ulong)lhs * (ulong)rhs);
        case RawOperation.BAND:
            return (long)((ulong)lhs & (ulong)rhs);
        case RawOperation.BOR:
            return (long)((ulong)lhs | (ulong)rhs);
        case RawOperation.BXOR:
            return (long)((ulong)lhs ^ (ulong)rhs);
        case RawOperation.SHL:
            return (long)((ulong)lhs << (int)rhs);
        case RawOperation.SHR:
            return (long)((ulong)lhs >> (int)rhs);
        default:
            throw new LuaNotSupportedError($"Unsupported operation '{op}' in RawOperate.");
        }
    }
    public static long Arith(Op op, long lhs, long rhs) { /* 没有 POW 和 DIV */
        switch (op) {
        case Op.ADD:
            return RawOperate(RawOperation.ADD, lhs, rhs);
        case Op.SUB:
            return RawOperate(RawOperation.SUB, lhs, rhs);
        case Op.MUL:
            return RawOperate(RawOperation.MUL, lhs, rhs);
        case Op.BAND:
            return RawOperate(RawOperation.BAND, lhs, rhs);
        case Op.BOR:
            return RawOperate(RawOperation.BOR, lhs, rhs);
        case Op.BXOR:
            return RawOperate(RawOperation.BXOR, lhs, rhs);
        case Op.UNM:
            return RawOperate(RawOperation.SUB, 0, lhs);
        case Op.BNOT:
            return RawOperate(RawOperation.BXOR, ~0, lhs);
        case Op.MOD:
            return Mod(lhs, rhs);
        case Op.IDIV:
            return Div(lhs, rhs);
        case Op.SHL:
            return LeftShift(lhs, rhs);
        case Op.SHR:
            return LeftShift(lhs, -rhs);
        default:
            LuaDebug.Check(false, $"Unsupported operation '{op}' in int arith.");
            return 0;
        }
    }
    public static double Arith(Op op, double lhs, double rhs) {
        switch (op) {
        case Op.ADD:
            return lhs + rhs;
        case Op.SUB:
            return lhs - rhs;
        case Op.MUL:
            return lhs * rhs;
        case Op.DIV:
            return lhs / rhs;
        case Op.POW:
            return Math.Pow(lhs, rhs);
        case Op.IDIV:
            return Math.Floor(lhs / rhs);
        case Op.UNM:
            return -lhs;
        case Op.MOD:
            return lhs - Math.Floor(lhs / rhs) * rhs;
        default:
            LuaDebug.Check(false, $"Unsupported operation '{op}' in number arith.");
            return 0;
        }
    }
    public static bool Arith(Op op, in LuaValue lhs, in LuaValue rhs, out LuaValue result) {
        double x, y;
        switch (op) {
        case Op.BAND:
        case Op.BOR:
        case Op.BXOR:
        case Op.SHL:
        case Op.SHR:
        case Op.BNOT:
            if (lhs.ToInteger(out long i) && rhs.ToInteger(out long j)) {
                result = new LuaValue(Arith(op, i, j));
                return true;
            }
            break;
        case Op.DIV:
        case Op.POW:
            if (lhs.ToNumber(out x) && rhs.ToNumber(out y)) {
                result = new LuaValue(Arith(op, x, y));
                return true;
            }
            break;
        default:
            if (lhs.IsInt && rhs.IsInt) {
                result = new LuaValue(Arith(op, lhs.Int, rhs.Int));
                return true;
            } else if (lhs.ToNumber(out x) && rhs.ToNumber(out y)) {
                result = new LuaValue(Arith(op, x, y));
                return true;
            }
            break;
        }
        result = LuaValue.NONE;
        return false;
    }
    public static LuaValue Arith(Op op, in LuaValue lhs, in LuaValue rhs) {
        if (Arith(op, lhs, rhs, out LuaValue result))
            return result;
        throw new LuaRuntimeError($"Unsupported operation '{op}' betwewen '{lhs}' and '{rhs}'.");
    }
}

/* 值运算的元表版本 */
public partial class LuaState
{
    internal bool Equals(in LuaValue lhs, in LuaValue rhs) {
        if (lhs.Equals(rhs))
            return true;

        /* raw equal 为 false，尝试对个别类型用元方法比较 */
        if (lhs.Type.Raw != rhs.Type.Raw)
            return false;
        else if (!LuaType.CheckTag(lhs, LuaConst.TUSERDATA, LuaConst.TTABLE))
            return false; /* 只有 userdata 和 table 会尝试获取元方法进行比较 */

        LuaValue tagMethod = LuaValue.NONE;
        switch (lhs.Type.NotNoneVariant) {
        case LuaConst.TTABLE:
            tagMethod = FastGetTagMethod(lhs.LObject<LuaTable>().Metatable, TagMethod.EQ);
            if (tagMethod.Null)
                tagMethod = FastGetTagMethod(rhs.LObject<LuaTable>().Metatable, TagMethod.EQ);
            break;
        case LuaConst.TUSERDATA:
            tagMethod = FastGetTagMethod(lhs.LObject<LuaUserData>().Metatable, TagMethod.EQ);
            if (tagMethod.Null)
                tagMethod = FastGetTagMethod(rhs.LObject<LuaUserData>().Metatable, TagMethod.EQ);
            break;
        }
        if (tagMethod.Null) /* no TM? */
            return false;   /* objects are different */
        CallTagMethodWithResult(tagMethod, lhs, rhs, _top);
        return _stack[(int)_top].ToBoolean();
    }
    internal bool LessThan(in LuaValue lhs, in LuaValue rhs) {
        if (lhs.Type.NotNoneTag == rhs.Type.NotNoneTag) {
            switch (lhs.Type.NotNoneTag) {
            case LuaConst.TNUMBER:
                return LuaUtils.NumberLessThan(lhs, rhs);
            case LuaConst.TSTRING:
                return string.Compare(lhs.Str, rhs.Str) < 0;
            }
        }
        if (TryOrderTagMethod(lhs, rhs, TagMethod.LT, out bool result)) {
            return result;
        }
        throw new LuaCompareError(lhs, rhs);
    }
    internal bool LessEqual(in LuaValue lhs, in LuaValue rhs) {
        if (lhs.Type.NotNoneTag == rhs.Type.NotNoneTag) {
            switch (lhs.Type.NotNoneTag) {
            case LuaConst.TNUMBER:
                return LuaUtils.NumberLessEqual(lhs, rhs);
            case LuaConst.TSTRING:
                return string.Compare(lhs.Str, rhs.Str) <= 0;
            }
        }
        if (TryOrderTagMethod(lhs, rhs, TagMethod.LE, out bool result)) {
            return result;
        } else { /* 尝试用 LessThan 实现 LessEqual */
            _currCI.SetCallStatusFlag(CallStatus.LEQ, true);
            bool success = TryOrderTagMethod(rhs, lhs, TagMethod.LT, out result); /* rhs < lhs */
            _currCI.SetCallStatusFlag(CallStatus.LEQ, false);
            if (success)
                return !result; /* 结果是 !(rhs < lhs) */
        }
        throw new LuaCompareError(lhs, rhs);
    }
    internal void Concat(int total) {
        LuaDebug.Assert(total >= 2, $"Invalid total '{total}' in Concat.");
        StringBuilder sb = null;
        do {
            int         n      = 2; /* 本次循环将要合并的数量 */
            LuaValue    lhs    = _stack[(int)_top - 2];
            LuaValue    rhs    = _stack[(int)_top - 1];
            StackRawPtr lhsptr = new StackRawPtr(this, _top - 2);
            StackRawPtr rhsptr = new StackRawPtr(this, _top - 1);
            if (!(lhs.IsString || lhs.CanConvertToString) || !LuaValue.TryConvertToString(rhsptr)) {
                /* lhs 或 rhs 无法转为字符串的情况 */
                CallBinTagMethod(lhs, rhs, _top - 2, TagMethod.CONCAT); /* 注意，该调用不会改变 _top 的位置 */
            } else if (rhs.Type.Tag == LuaConst.TSTRING && rhs.LObject<LuaString>().UTF8Length == 0) {
                /* rhs 为空字符串的情况，尝试将 lhs 转为 string */
                LuaValue.TryConvertToString(lhsptr);
            } else if (lhs.Type.Tag == LuaConst.TSTRING && lhs.LObject<LuaString>().UTF8Length == 0) {
                /* lhs 为空字符串的情况，rhs 为非空字符串，直接移动 rhs 到结果位置即可 */
                _stack[(int)_top - 2] = _stack[(int)_top - 1];
            } else {
                /* lhs 和 rhs 均为非空字符串的情况，尝试把“左边”待连接的对象也一起合并了 */
                if (sb == null)
                    sb = new StringBuilder();
                sb.Clear();
                for (n = 0; n < total && LuaValue.TryConvertToString(new StackRawPtr(this, _top - n - 1)); n++) /* 右结合，从右往左合并 */
                    sb.Insert(0, _stack[(int)_top - n - 1].Str);
                _stack[(int)_top - n] = new LuaValue(sb.ToString()); /* 最左边的元素被赋予最终值 */
            }
            total -= n - 1;
            _top -= n - 1;
        } while (total > 1);
    }
    internal void GetLength(in LuaValue target, RawIdx output) {
        LuaValue tagMethod;
        switch (target.Type.NotNoneTag) {
        case LuaConst.TTABLE:
            LuaTable table = target.LObject<LuaTable>();
            tagMethod      = FastGetTagMethod(table.Metatable, TagMethod.LEN);
            if (tagMethod.Null) {
                _stack[(int)output] = new LuaValue(table.GetArrayLength());
                return;
            }
            break;
        case LuaConst.TSTRING:
            _stack[(int)output] = new LuaValue(target.LObject<LuaString>().UTF8Length);
            return;
        default:
            tagMethod = GetTagMethod(target, TagMethod.LEN);
            if (tagMethod.Null)
                throw new LuaRuntimeError($"Can't get length of this object: {target}");
            break;
        }
        CallTagMethodWithResult(tagMethod, target, target, output);
    }

    internal void Arith(Op op, in LuaValue lhs, in LuaValue rhs, out LuaValue result) {
        if (LuaOperation.Arith(op, lhs, rhs, out result))
            return;
        CallBinTagMethod(lhs, rhs, _top, LuaConst.OP_TO_TAGMETHOD[(int)op]);
        result = _stack[(int)_top];
    }
    internal LuaValue Arith(Op op, in LuaValue lhs, in LuaValue rhs) {
        Arith(op, lhs, rhs, out LuaValue result);
        return result;
    }
    internal void Arith(Op op, in LuaValue lhs, in LuaValue rhs, RawIdx output) {
        _stack[(int)output] = Arith(op, lhs, rhs);
    }
}

}
