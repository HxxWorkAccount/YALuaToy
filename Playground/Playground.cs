using Xunit;
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using YALuaToy;
using YALuaToy.Core;
using YALuaToy.Debug;
using YALuaToy.Const;
using YALuaToy.Compilation;
using YALuaToy.StandardLibrary;
using YALuaToy.Tests.Utils;
using YALuaToy.Tests.Utils.Mock;
using YALuaToy.Tests.Utils.Test;
using YALuaToy.Compilation.Antlr;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime;

class Playground
{
    static void TestLua() {
        /* project config */
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        Trace.AutoFlush = true;

        LuaState state = LuaState.NewState("test", "out/LuaLog");
        state.OpenSTD();

        string filepath = "Assets/Lua/Tests/coroutine.lua";
        // string           filepath     = "Assets/Lua/misc.lua";
        string           fileText     = File.ReadAllText(filepath);
        AntlrInputStream inputStream  = new AntlrInputStream(fileText);
        ThreadStatus     threadStatus = LuaParserUtils.Parse(state, inputStream, filepath);
        if (threadStatus != ThreadStatus.OK) {
            Console.WriteLine($"[{threadStatus}] {state.ToString(-1, out _)}");
            return;
        }

        /* 反编译 */
        LClosure lclosure  = state.GetStack(state.Top - 1).LObject<LClosure>();
        FileIR   fileIR1   = LuacUtils.DecompileByLuac(filepath);
        FileIR   fileIR2   = LuacUtils.Decompile(lclosure.proto, filepath);
        string   cachepath = $"out/DecompileCache/{Path.GetFileNameWithoutExtension(filepath)}.cslua";
        File.WriteAllText(cachepath, fileIR2.ToString());

        /* 执行 */
        int PrintError(LuaState state_) {
            Console.WriteLine($"\n[{state_.prevThreadStatus}] {state_.ToString(-1, out _)}");
            Console.WriteLine("traceback: ");
            Console.WriteLine(state_.Traceback());
            return 1;
        }
        state.Push(PrintError); /* 通过 PrintError 打印错误信息，因为 PCall 遇到错误时会 shrink ci 链，导致调用链信息丢失 */
        state.Insert(-2);
        threadStatus = state.PCall(0, LuaConst.MULTRET, -2);
        if (threadStatus != ThreadStatus.OK)
            return;

        state.CloseState(); /* 释放状态机，刷新 log 缓冲 */
        Console.WriteLine("OK!");
    }

    static void Main(string[] args) {
        // TestLua();
        List<int> list = new List<int>();
        list.Add(1);
        Console.WriteLine(list[2]);
    }
}
