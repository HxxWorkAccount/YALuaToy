namespace YALuaToy.Const {

using System;

public static class LuaConst
{
    public const string VERSION_MAJOR   = "5";
    public const string VERSION_MINOR   = "3";
    public const double VERSION_NUM     = 503;
    public const string VERSION_RELEASE = "6";

    public const string VERSION = "Lua " + VERSION_MAJOR + "." + VERSION_MINOR;
    public const string RELEASE = VERSION + "." + VERSION_RELEASE;

    /* mark for precompiled code ('<esc>Lua') */
    public const string SIGNATURE = "\x1bLua";

    /* option for multiple returns in 'lua_pcall' and 'lua_call' */
    public const int MULTRET = -1;

    /* Pseudo-indices */
    public const int  REGISTRYINDEX               = -LuaConfig.LUAI_MAXSTACK - 1000;
    public static int UpvalueLdx(int upvalueLdx) => REGISTRYINDEX - upvalueLdx;

    /* minimum Lua stack available to a C# function */
    public const int MINSTACK = 20;

    /* predefined values in the registry */
    public const int RIDX_MAINTHREAD = 1; /* Main Thread */
    public const int RIDX_GLOBALS    = 2; /* _G */
    public const int RIDX_LAST       = RIDX_GLOBALS;

    /*
     * lua type
     * bits 0-3: actual tag (a LUA_T* value)
     * bits 4-5: variant bits
     * bit 6: is LuaObject? (clua: whether value is collectable)
     */

    /*
     * CLua 里，TNONE 用于：lua_type 接口当获取到无效对象时，返回的类型。没有其他用处了
     * 但在 YALuaToy 里，由于没法给 struct 取指针，所以用 TNONE 类型表示无返回值的情况
     * （即，CLua 的 lapi.isvalid 接口用途，判断对象地址是否等于全局的 luaO_nilobject）
     * 理论上，最好不使用 TNONE，只应用于“需要和 nil 区分”的情景
     *
     * 吐槽：这个 TNONE = -1 把一切都搞得很复杂。。。但为了兼容 C 又不得不这么写
     */
    public const sbyte TNONE          = -1; /* 这边用 TNONE 来表示 luaO_nilobject（即无返回值，而不是 nil） */
    public const sbyte TNIL           = 0;
    public const sbyte TBOOLEAN       = 1;
    public const sbyte TLIGHTUSERDATA = 2;
    public const sbyte TNUMBER        = 3;
    public const sbyte TSTRING        = 4;
    public const sbyte TTABLE         = 5;
    public const sbyte TFUNCTION      = 6;
    public const sbyte TUSERDATA      = 7;
    public const sbyte TTHREAD        = 8;
    public const sbyte NUMTAGS        = 9; /* Valid Type 数量 */
    /* internal type tag */
    internal const sbyte TPROTO = 9;
    // internal const sbyte TDEADKEY  = 10; /* 这个用不了，C# Dictionary 无法改变键 */
    internal const sbyte TOTALTAGS = 11;
    /* variant type */
    internal const sbyte TLCL         = TFUNCTION | (0 << 4); /* lua closure */
    internal const sbyte TLCF         = TFUNCTION | (1 << 4); /* light C# function */
    internal const sbyte TCCL         = TFUNCTION | (2 << 4); /* C# closure */
    internal const sbyte TNORMALSTR   = TSTRING | (0 << 4);   /* normal string */
    internal const sbyte TRESERVEDSTR = TSTRING | (1 << 4);   /* reserved string */
    // internal const sbyte TSHRSTR = TSTRING | (0 << 4);   /* short strings */ // 不区分长短字符串，字符串的优化由 C# 管理
    // internal const sbyte TLNGSTR = TSTRING | (1 << 4);   /* long strings */
    internal const sbyte TNUMFLT = TNUMBER | (0 << 4); /* float numbers */
    internal const sbyte TNUMINT = TNUMBER | (1 << 4); /* integer numbers */

    internal const sbyte  BIT_ISLUAOBJECT            = 1 << 6; /* 第 7 位用于表示 LuaObject 标记 */
    internal static sbyte MarkLuaObject(sbyte type) => (sbyte)(type | BIT_ISLUAOBJECT);
    internal static sbyte TagPart(sbyte type)       => (sbyte)(type & 0x0F);
    internal static sbyte VariantPart(sbyte type)   => (sbyte)(type & 0x3F);

    internal static readonly string
    [] TYPENAMES = { "none", "nil", "boolean", "lud", "number", "string", "table", "function", "userdata", "thread", "proto", "deadkey" };

    internal static string TypeName(sbyte type, bool variant = false) {
        if (type < TOTALTAGS && (!variant || (type != TNUMBER && type != TSTRING && type != TFUNCTION)))
            return TYPENAMES[type + 1];
        switch (VariantPart(type)) {
        case TLCL:
            return "lclosure";
        case TLCF:
            return "cfunc";
        case TCCL:
            return "cclosure";
        case TNUMFLT:
            return "float";
        case TNUMINT:
            return "int";
        case TNORMALSTR:
            return "string";
        case TRESERVEDSTR:
            return "resstr";
        default:
            return "unknown";
        }
    }

    internal static readonly string[] TAG_METHOD_NAMES = {
        "__index", "__newindex", "__gc",  "__mode", "__len", "__eq",  "__add", "__sub",  "__mul", "__mod", "__pow",    "__div",
        "__idiv",  "__band",     "__bor", "__bxor", "__shl", "__shr", "__unm", "__bnot", "__lt",  "__le",  "__concat", "__call"
    };

    internal static readonly TagMethod[] OP_TO_TAGMETHOD = {
        TagMethod.ADD,  TagMethod.SUB, TagMethod.MUL,  TagMethod.MOD, TagMethod.POW, TagMethod.DIV, TagMethod.IDIV,
        TagMethod.BAND, TagMethod.BOR, TagMethod.BXOR, TagMethod.SHL, TagMethod.SHR, TagMethod.UNM, TagMethod.BNOT,
    };

    internal const string ENV = "_ENV";
}

public enum ThreadStatus {
    OK,
    YIELD,
    ERRRUN,    /* 运行时错误 */
    ERRSYNTAX, /* 语法错误 */
    ERRMEM,    /* 内存错误（暂时没用） */
    ERRGCMM,   /* GC 错误（暂时没用） */
    ERRERR,    /* 错误处理程序报错 */
}

public enum Op {
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
    UNM, /* 一元减 */
    BNOT,
}

public enum Comp {
    EQ,
    LT,
    LE,
}

internal enum TagMethod {
    INDEX,
    NEWINDEX,
    GC,
    MODE,
    LEN,
    EQ, /* last tag method with fast access */
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
    UNM,
    BNOT,
    LT,
    LE,
    CONCAT,
    CALL,
    N /* number of elements in the enum */
}

[Flags]
internal enum CallStatus {
    ORIGIN_ALLOW_HOOK = 1 << 0,
    LUA               = 1 << 1,
    HOOKED            = 1 << 2,
    FRESH             = 1 << 3,
    YIELDABLE_PCALL   = 1 << 4,
    TAIL_CALL         = 1 << 5,
    HOOK_YIELD        = 1 << 6,
    LEQ               = 1 << 7,
    FIN               = 1 << 8,
}

internal enum ToIntMode {
    INT,   /* 只接受整型 */
    FLOOR, /* 向负无穷取整（截断小数） */
    CEIL,  /* 向 0 取整 */
}

}
