namespace YALuaToy.Tests.Smoke {

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using YALuaToy.Core;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Compilation;
using YALuaToy.Tests.Utils;
using YALuaToy.Tests.Utils.Mock;
using YALuaToy.Tests.Utils.Test;
using Antlr4.Runtime;
using YALuaToy.Compilation.Antlr;
using YALuaToy.StandardLibrary;

public class LuaIntegrationTests
{
    private readonly ITestOutputHelper _output;
    public LuaIntegrationTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Theory]
    [InlineData("Assets/Lua/Tests/bitwise.lua")]
    [InlineData("Assets/Lua/Tests/bwcoercion.lua")]
    [InlineData("Assets/Lua/Tests/calls.lua")]
    [InlineData("Assets/Lua/Tests/closure.lua")]
    [InlineData("Assets/Lua/Tests/constructs.lua")]
    [InlineData("Assets/Lua/Tests/coroutine.lua")]
    [InlineData("Assets/Lua/Tests/events.lua")]
    [InlineData("Assets/Lua/Tests/goto.lua")]
    [InlineData("Assets/Lua/Tests/locals.lua")]
    [InlineData("Assets/Lua/Tests/misc.lua")]
    [InlineData("Assets/Lua/Tests/nextvar.lua")]
    [InlineData("Assets/Lua/Tests/vararg.lua")]
    public void ExpectCorrect(string filepath) {
        filepath                      = CommonTestUtils.GetPath(filepath);
        string           fileText     = File.ReadAllText(filepath);
        LuaState         state        = LuaState.NewState("test", "out/LuaLog");
        AntlrInputStream inputStream  = new AntlrInputStream(fileText);
        ThreadStatus     threadStatus = LuaParserUtils.Parse(state, inputStream, filepath);
        if (threadStatus != ThreadStatus.OK) {
            Console.WriteLine($"[{threadStatus}] {state.ToString(-1, out _)}");
            return;
        }

        /* Open Library */
        state.OpenSTD();

        // /* 反编译 */
        // LClosure lclosure = state.GetStack(state.Top - 1).LObject<LClosure>();
        // FileIR   fileIR1   = LuacUtils.DecompileByLuac(filepath);
        // FileIR fileIR2   = LuacUtils.Decompile(lclosure.proto, filepath);
        // string cachepath = $"out/DecompileCache/{Path.GetFileNameWithoutExtension(filepath)}.cslua";
        // File.WriteAllText(cachepath, fileIR2.ToString());

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
}

}
