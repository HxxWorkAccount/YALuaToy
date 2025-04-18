namespace YALuaToy.Interpreter {
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using YALuaToy.Core;
using YALuaToy.Const;
using YALuaToy.StandardLibrary;

static class Interpreter
{
    static readonly Random random          = new();
    static readonly        string[] hellos = [
        "(˶˃ ᵕ ˂˶)",
        "≽^•⩊•^≼",
        "(≧◡≦) ♡",
        "(⸝⸝⸝>﹏<⸝⸝⸝)",
        "꒰ঌ(˶ˆᗜˆ˵)໒꒱",
        "✧٩(•́⌄•́๑)و ✧",
        "(・ωｰ)～☆",
        "ლ(╹◡╹ლ)",
        "o(*≧▽≦)ツ┏━┓",
    ];

    /* provided by chatgpt o3-mini ... */
    static readonly List<string> history = [];
    static string                PowerfulReadLine() {
        StringBuilder buffer       = new StringBuilder();
        int           pos          = 0;
        int           historyIndex = history.Count;
        string        prompt       = ">";

        void RedrawFrom() {
            /* 重新输出从 pos 到末尾的字符，然后输出空格消除尾部残余，并调整光标位置 */
            string tail = buffer.ToString(pos, buffer.Length - pos);
            Console.Write(tail + " ");
            Console.CursorLeft -= tail.Length + 1;
        }

        /* 清空当前输入显示区域（假设行首有提示符"> "） */
        void ClearInput() {
            int currentPos     = Console.CursorLeft;
            Console.CursorLeft = 0;                              /* 回退到行首 */
            Console.Write(new string(' ', Console.WindowWidth)); /* 重写空白覆盖原内容，再回到行首 */
            Console.CursorLeft = 0;
        }

        void RedrawLine() { /* 重绘整个输入行（包含提示符） */
            ClearInput();
            Console.Write($"{prompt} " + buffer.ToString());
            Console.CursorLeft = 2 + pos; /* 将光标置于 pos 后面，加上提示符字符数 */
        }

        RedrawLine();
        while (true) {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) {
                /* 先不支持 shift 多行了，感觉跨平台有点问题。目前只支持单行操作 */
                // if (false || (key.Modifiers & ConsoleModifiers.Shift) != 0) {
                //     buffer.Append(' '); /* 将当前行“连接”为输入的一部分（用空格分隔） */
                //     pos = buffer.Length;
                //     Console.WriteLine();
                //     prompt = ">>"; /* 换行并将提示符改为续行提示符：">> " */
                //     RedrawLine();
                //     continue;
                // } else {
                //     Console.WriteLine();
                //     break;
                // }
                Console.WriteLine();
                break;
            } else if (key.Key == ConsoleKey.LeftArrow) {
                if (pos > 0) {
                    pos--;
                    Console.CursorLeft--;
                }
            } else if (key.Key == ConsoleKey.RightArrow) {
                if (pos < buffer.Length) {
                    pos++;
                    Console.CursorLeft++;
                }
            } else if (key.Key == ConsoleKey.Backspace) {
                if (pos > 0) {
                    buffer.Remove(pos - 1, 1);
                    pos--;
                    Console.CursorLeft--;
                    RedrawFrom();
                }
            } else if (key.Key == ConsoleKey.Delete) {
                if (pos < buffer.Length) {
                    buffer.Remove(pos, 1);
                    RedrawFrom();
                }
            } else if (key.Key == ConsoleKey.UpArrow) {
                if (history.Count > 0 && historyIndex > 0) {
                    historyIndex--;
                    // 用历史记录替换当前输入
                    buffer.Clear();
                    buffer.Append(history[historyIndex]);
                    pos = buffer.Length;
                    RedrawLine();
                }
            } else if (key.Key == ConsoleKey.DownArrow) {
                if (history.Count > 0 && historyIndex < history.Count) {
                    historyIndex++;
                    buffer.Clear();
                    // 当索引等于历史记录数量时，表示最新的一行（空行）
                    if (historyIndex == history.Count)
                        pos = 0;
                    else {
                        buffer.Append(history[historyIndex]);
                        pos = buffer.Length;
                    }
                    RedrawLine();
                }
            } else {
                buffer.Insert(pos, key.KeyChar);
                Console.Write(buffer.ToString(pos, buffer.Length - pos));
                pos++;
                int remaining = buffer.Length - pos; /* 将光标移动回插入字符后的正确位置 */
                Console.CursorLeft -= remaining;
            }
        }
        string result = buffer.ToString();
        if (!string.IsNullOrWhiteSpace(result)) {
            history.Add(result);
            if (history.Count > 100) /* 限制历史记录数量 */
                history.RemoveAt(0);
        }
        return result;
    }

    static int PrintError(LuaState state) {
        Console.WriteLine($"\n[{state.PrevThreadStatus}] {state.ToString(-1, out _)}");
        Console.WriteLine("traceback: ");
        Console.WriteLine(state.Traceback());
        return 1;
    }

    static int PMain(LuaState state) {
        /* 支持多行输入 */
        state.OpenSTD();
        if (state.TopLdx == 0) {    /* 交互模式 */
            state.Push(PrintError); /* 先压入错误打印函数 */
            Console.WriteLine($"YALuaToy 5.3.6  {hellos[random.Next(hellos.Length)]}");
            while (true) {
                string? line = PowerfulReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                state.TopLdx = 1; /* 清空旧数据 */

                /* 加载输入的 Lua 代码，会先尝试添加 return */
                ThreadStatus threadStatus = state.LoadString("return " + line);
                if (threadStatus != ThreadStatus.OK) {
                    state.TopLdx = 1;
                    threadStatus = state.LoadString(line);
                    if (threadStatus != ThreadStatus.OK) {
                        Console.WriteLine($"\n[{threadStatus}] {state.ToString(-1, out _)}");
                        continue;
                    }
                }

                /* 调用执行代码（无参数，多返回值） */
                threadStatus = state.PCall(0, LuaConst.MULTRET, 1);

                /* 打印返回值 */
                if (threadStatus == ThreadStatus.OK) {
                    int elemCount = state.TopLdx; /* 注意第一个元素是 printerror 函数 */
                    for (int i = 2; i <= elemCount; i++)
                        Console.WriteLine(state.GetValueString(i)); /* 这里用内部的 ToString */
                }
            }
        } else { /* 解释文件 */
            string       filepath     = state.ToString(-1, out _);
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            ThreadStatus threadStatus = state.LoadFile(filepath);
            if (threadStatus != ThreadStatus.OK)
                Console.WriteLine($"\n[{threadStatus}] {state.ToString(-1, out _)}");
            else
                state.Call(0, LuaConst.MULTRET);
            state.Push(threadStatus == ThreadStatus.OK);
        }
        return 1;
    }

    /* 只提供两个功能：要么指定 .lua 文件，要么交互模式 */
    static int Main(string[] args) {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        Trace.AutoFlush = true;

        LuaState state = LuaState.NewState();
        if (state == null)
            throw new Exception("cannot create state");

        state.Push(PrintError);
        state.Push(PMain);
        if (args.Length > 0)
            state.Push(args[0]);
        ThreadStatus threadStatus = state.PCall((short)(args.Length > 0 ? 1 : 0), LuaConst.MULTRET, 1);

        if (threadStatus != ThreadStatus.OK || !state.ToBoolean(-1)) {
            Console.WriteLine($"\n\u001b[31mLua Failed\u001b[0m");
            state.CloseState();
            return 1;
        }
        state.CloseState();
        return 0;
    }
}
}
