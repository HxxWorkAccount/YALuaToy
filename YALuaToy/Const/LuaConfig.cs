namespace YALuaToy.Const {
using System.Runtime.InteropServices;
using YALuaToy.Core;

public static class LuaConfig
{
    public const int              LUAI_BITSINT  = 32;
    public const int              LUAI_MAXSTACK = 1000000;
    public static readonly string LUA_DIRSEP    = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\\" : "/";
    public const string           LUA_PATH_SEP  = ";";
    public const string           LUA_PATH_MARK = "?";
    public const string           LUA_EXEC_DIR  = "!";

    public static readonly string LUA_VDIR = $"{LuaConst.VERSION_MAJOR}.{LuaConst.VERSION_MINOR}";
#if WINDOWS
    /* In Windows, any exclamation mark ('!') in the path is replaced by the
       path of the directory of the executable file of the current process. */
    public const string           LUA_LDIR         = "!\\lua\\";
    public const string           LUA_CDIR         = "!\\";
    public static readonly string LUA_SHRDIR       = $"!\\..\\share\\lua\\{LUA_VDIR}\\";
    public static readonly string LUA_PATH_DEFAULT =  //
        $"{LUA_LDIR}?.lua;" +                         //
        $"{LUA_LDIR}?\\init.lua;" +                   //
        $"{LUA_CDIR}?\\init.lua;" +                   //
        $"{LUA_SHRDIR} ?.lua;" +                      //
        $"{LUA_SHRDIR}?\\init.lua;" +                 //
        ".\\?.lua;" +                                 //
        ".\\?\\init.lua";
#else
    public const string           LUA_ROOT         = "/usr/local/";
    public static readonly string LUA_LDIR         = $"{LUA_ROOT}share/lua/{LUA_VDIR}/";
    public static readonly string LUA_CDIR         = $"{LUA_ROOT}lib/lua/{LUA_VDIR}/";
    public static readonly string LUA_PATH_DEFAULT =  //
        $"{LUA_LDIR}?.lua;" +                         //
        $"{LUA_LDIR}?/init.lua;" +                    //
        $"{LUA_CDIR}?.lua;" +                         //
        $"{LUA_CDIR}?/init.lua;" +                    //
        "./?.lua;" +                                  //
        "./?/init.lua";
#endif

    /* ---------------- Internal Config ---------------- */

    /* Lua State */
    internal const int EXTRA_STACK       = 5; /* 个别情况的栈空间额外大小（如：处理 tag method 时） */
    internal const int BASIC_STACK_SIZE  = 2 * LuaConst.MINSTACK;
    internal const int MAX_UPVALUE_COUNT = 255; /* 闭包内上值最大数量 */
    internal const int ERROR_STACK_SIZE  = LUAI_MAXSTACK + 200;

    internal const int LUA_TABLE_SWEEP_THRESHOLD = 20;

    internal const int LUAI_MAXCCALLS = 200; /* 该配置最大不能超过 255 */

    internal const ToIntMode DEFAULT_TO_INT_MODE = ToIntMode.INT; /* 默认不允许浮点转整型 */

    internal const int MAX_TAG_LOOP = 2000;

    internal const int LFIELDS_PER_FLUSH = 50; /* 读取列表字面值时的写入缓冲数量 */

    /* 这个最大我感觉应该是 254，因为 0 号位是被函数占用的 */
    internal const int MAX_REG_COUNT = 254; /* 目前实现最多只支持 255，再大的话很多地方的类型要改 */
}

}
