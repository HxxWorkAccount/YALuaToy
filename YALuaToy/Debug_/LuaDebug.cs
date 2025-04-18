namespace YALuaToy.Debug {

using System;
using System.Diagnostics;
using YALuaToy.Core;
using YALuaToy.Const;
using System.Reflection;
using System.Text;
using YALuaToy.Compilation;
using System.Linq;
using System.IO;

internal static class LuaDebug
{
    private static bool _muteCheck = false;

    public static void QuietAction(Action action) {
        _muteCheck = true;
        action();
        _muteCheck = false;
    }

    [Conditional("DEBUG")]
    public static void Check(bool condition, string msg = null) {
        if (condition || _muteCheck)
            return;
        if (msg != null)
            Debug.WriteLine($"\u001b[31m[Check Failure] {msg}\u001b[0m");
        else
            Debug.WriteLine($"\u001b[31m[Check Failure]\u001b[0m");
        // Debug.Assert(false, msg != null ? msg : "");
        Traceback(1, depth: 10);
    }

    [Conditional("DEBUG")]
    public static void Assert(bool condition, string msg = null) {
        if (condition)
            return;
        if (msg != null)
            Debug.WriteLine($"\u001b[31m[Assert Failure] {msg}\u001b[0m");
        else
            Debug.WriteLine($"\u001b[31m[Assert Failure]\u001b[0m");
        // Traceback(1); /* Debug.Assert 也会打印调用栈 */
        Debug.Assert(false, msg != null ? msg : "");
    }

    public static void Traceback(ushort skipFrames = 0, ushort depth = 5) {
        skipFrames++; /* 忽略 Traceback 自身 */
        if (depth == 0)
            depth = ushort.MaxValue;

        StackTrace trace    = new StackTrace(skipFrames, true);
        StackFrame[] frames = trace.GetFrames();
        int lastFrame       = Math.Min(frames.Length - 1, depth - 1);
        if (frames.Length == 0) {
            Debug.WriteLine($"Traceback failed, skipFrames too large: {skipFrames}, depth: {depth}, frameLength: {frames.Length}.");
            return;
        }

        Debug.WriteLine("Traceback (most recent call last):");
        Debug.Indent();
        if (frames != null) {
            for (int i = lastFrame; i >= 0; i--) {
                StackFrame frame    = frames[i];
                string     filename = frame.GetFileName();
                int        line     = frame.GetFileLineNumber();
                int        col      = frame.GetFileColumnNumber();
                if (string.IsNullOrEmpty(filename))
                    continue;
                // DiagnosticMethodInfo methodInfo = DiagnosticMethodInfo.Create(frame);
                MethodBase methodInfo = frame.GetMethod();
                Debug.WriteLine($"File '{filename}', line {line}, col {col} in {methodInfo.Name}.");
            }
        }
        Debug.Unindent();
    }

    [Conditional("DEBUG")]
    public static void AssertNotNull(object obj, string msg = null) {
        if (msg == null)
            msg = "Expected not null.";
        Assert(obj != null, msg);
    }

    [Conditional("DEBUG")]
    public static void AssertTag(in LuaValue luaValue, params LuaType[] types) {
        AssertTag(luaValue.Type, types);
    }
    [Conditional("DEBUG")]
    public static void AssertTag(LuaType luaType, params LuaType[] types) {
        Assert(LuaType.CheckTag(luaType, types), $"Unexpected type, current: {luaType.Raw}, expected: {string.Join(", ", types)}.");
    }
    [Conditional("DEBUG")]
    public static void AssertVariant(in LuaValue luaValue, params LuaType[] types) {
        AssertVariant(luaValue.Type, types);
    }
    [Conditional("DEBUG")]
    public static void AssertVariant(LuaType luaType, params LuaType[] types) {
        Assert(LuaType.CheckVariant(luaType, types), $"Unexpected type, current: {luaType.Raw}, expected: {string.Join(", ", types)}.");
    }

    [Conditional("DEBUG")]
    public static void AssertNotNone(LuaType luaType) {
        Assert(luaType.Raw != LuaConst.TNONE, $"None type is not allow.");
    }

    [Conditional("DEBUG")]
    public static void AssertLuaObject(LuaType luaType) {
        Assert(luaType.IsLuaObject, $"Unexpected type, current: {luaType.Raw}, expected LuaObject.");
    }

    [Conditional("DEBUG")]
    public static void AssertCanCreate(LuaType luaType) {
        /* 类型检查，排出如 nil、none、bool、deadkey 这些东西 */
        Assert(
            !LuaType.CheckTag(luaType, LuaConst.TNIL, LuaConst.TNONE, LuaConst.TBOOLEAN),
            $"Can't create instance for type '{LuaConst.TypeName(luaType.Variant)}'."
        );
    }

    [Conditional("DEBUG")]
    public static void AssertCanCacheTagMethod(TagMethod tag) {
        Assert(tag <= TagMethod.EQ, $"Expected: tag <= TagMethod.EQ, current tag: {tag}.");
    }

    [Conditional("DEBUG")]
    public static void AssertValidUpvalueLdx(int upvalueLdx) {
        Assert(upvalueLdx > 0 && upvalueLdx <= LuaConfig.MAX_UPVALUE_COUNT, $"Upvalue index too large: {upvalueLdx}.");
    }

    [Conditional("DEBUG")]
    public static void AssertValidEnum<TEnum>(TEnum e)
        where          TEnum : struct, Enum {
        Assert(Enum.IsDefined(e), $"Undefined enum value: {e}.");
    }

    [Conditional("DEBUG")]
    public static void AssertInstExtraarg(Instruction inst) {
        OpCode curr = inst.OpCode;
        Assert(curr == OpCode.EXTRAARG, $"Expected next instruction is extraarg, curr: {curr}");
    }

    [Conditional("DEBUG")]
    public static void AssertExpType(ExpDesc e, params ExpType[] expects) {
        AssertExpType(e.Type, expects);
    }
    [Conditional("DEBUG")]
    public static void AssertExpType(ExpType expType, params ExpType[] expects) {
        Assert(expects.Contains(expType), $"Expected: {string.Join(", ", expects)}, current: {expType}.");
    }
}

internal static class LuaDebugUtils
{
    public static void PrintStack(LuaState state) {
        StringBuilder sb        = new StringBuilder();
        CallInfo      ci        = state.CurrCI;
        RawIdx        stackbase = (RawIdx)0;

        Debug.WriteLine(ci); /* 输出当前调用帧信息 */

        /* 从栈顶开始向下打印 */
        for (RawIdx curr = state.Top - 1; curr > stackbase; curr--) {
            /* 计算相对于栈底和当前帧固定区域的偏移量 */
            int diffBase = curr - stackbase;
            int diffFunc = curr - ci.Func + 1;

            /* 格式化固定宽度输出（右对齐宽度为 2） */
            sb.Append(string.Format("[{0,2},{1,2}] ", diffBase, diffFunc));

            string mid;
            if (curr == ci.Top) {
                mid = "-".PadRight(7); /* 表示该元素恰好为当前调用帧的栈顶 */
            } else if (curr == ci.Func) {
                mid = (ci.IsLua ? "lfunc" : "cfunc").PadRight(7); /* 当栈索引等于调用帧中函数的位置，判断函数类型 */
                ci  = ci.prev;                                    /* 切换到前一个调用帧 */
            } else {
                mid = "".PadRight(7);
            }
            sb.Append(mid);

            try {
                /* 输出栈中该位置的值，调用 ToString() 转换表示 */
                sb.Append(' ').Append(state.GetStack(curr).ToString());
            } catch (Exception) {
                /* 如果转换出错，则打印提示信息 */
                sb.Append(" ... <invalid item>");
            }
            sb.AppendLine();
        }
        Console.WriteLine(sb.ToString());
    }
}

public class LuaLogger : TextWriter
{
    public const long MAX_LOG_COUNT = 5000000;

    public readonly string      stateName;
    public readonly string      outputDir;
    private readonly TextWriter _consoleOut;
    private StreamWriter        _fileWriter;

    private long _logCount  = 0;
    private bool _enableLog = true;

    public override Encoding Encoding => Encoding.UTF8;

    public LuaLogger(string stateName, string outputDir) {
        this.stateName = stateName;
        this.outputDir = outputDir;

        /* create file writer */
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
        if (!File.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        string filepath = $"{outputDir}/{stateName} {timestamp}.log";
        _fileWriter = new StreamWriter(new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };

        /* wrap console out */
        _consoleOut = Console.Out;
        Console.SetOut(this);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _fileWriter?.Dispose();
            Console.SetOut(_consoleOut);
        }
        base.Dispose(disposing);
    }

    /* ---------------- Writer API ---------------- */

    public override void Flush() {
        _consoleOut.Flush();
        if (_enableLog)
            _fileWriter.Flush();
    }
    public override void Write(char value) {
        _consoleOut.Write(value);
        if (_enableLog) {
            _fileWriter.Write(value);
            _CheckLogCount();
        }
    }
    public override void Write(string value) {
        _consoleOut.Write(value);
        if (_enableLog) {
            _fileWriter.Write(value);
            _CheckLogCount();
        }
    }
    public override void WriteLine(string value) {
        _consoleOut.WriteLine(value);
        if (_enableLog) {
            _fileWriter.WriteLine(value);
            _CheckLogCount();
        }
    }

    public void Log(char value) {
        if (_enableLog) {
            _fileWriter.Write(value);
            _CheckLogCount();
        }
    }
    public void Log(string value) {
        if (_enableLog) {
            _fileWriter.Write(value);
            _CheckLogCount();
        }
    }
    public void LogLine(string from, string value) {
        if (_enableLog) {
            _fileWriter.WriteLine($"[{from}] {value}");
            _CheckLogCount();
        }
    }

    private void _CheckLogCount() {
        if (!_enableLog)
            return;
        _logCount++;
        if (_logCount > MAX_LOG_COUNT) {
            _enableLog = false;
            _fileWriter.Flush();
            _fileWriter.Close();
            _fileWriter = null;
        }
    }
}

}
