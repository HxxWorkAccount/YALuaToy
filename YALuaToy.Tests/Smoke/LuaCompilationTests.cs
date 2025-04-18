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

public class LuaCompilationTests
{
    private readonly ITestOutputHelper _output;
    public LuaCompilationTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Theory]
    [InlineData("Assets/Lua/TranslatorTests/all.lua")]
    [InlineData("Assets/Lua/TranslatorTests/api.lua")]
    [InlineData("Assets/Lua/TranslatorTests/attrib.lua")]
    [InlineData("Assets/Lua/TranslatorTests/big.lua")]
    [InlineData("Assets/Lua/TranslatorTests/bitwise.lua")]
    [InlineData("Assets/Lua/TranslatorTests/bwcoercion.lua")]
    [InlineData("Assets/Lua/TranslatorTests/calls.lua")]
    [InlineData("Assets/Lua/TranslatorTests/closure.lua")]
    [InlineData("Assets/Lua/TranslatorTests/code.lua")]
    [InlineData("Assets/Lua/TranslatorTests/constructs.lua")]
    [InlineData("Assets/Lua/TranslatorTests/coroutine.lua")]
    [InlineData("Assets/Lua/TranslatorTests/db.lua")]
    [InlineData("Assets/Lua/TranslatorTests/errors.lua")]
    [InlineData("Assets/Lua/TranslatorTests/events.lua")]
    [InlineData("Assets/Lua/TranslatorTests/files.lua")]
    [InlineData("Assets/Lua/TranslatorTests/gc.lua")]
    [InlineData("Assets/Lua/TranslatorTests/goto.lua")]
    [InlineData("Assets/Lua/TranslatorTests/heavy.lua")]
    [InlineData("Assets/Lua/TranslatorTests/literals.lua")]
    [InlineData("Assets/Lua/TranslatorTests/locals.lua")]
    [InlineData("Assets/Lua/TranslatorTests/main.lua")]
    [InlineData("Assets/Lua/TranslatorTests/math.lua")]
    [InlineData("Assets/Lua/TranslatorTests/nextvar.lua")]
    [InlineData("Assets/Lua/TranslatorTests/pm.lua")]
    [InlineData("Assets/Lua/TranslatorTests/sort.lua")]
    [InlineData("Assets/Lua/TranslatorTests/strings.lua")]
    // [InlineData("Assets/Lua/TranslatorTests/tpack.lua")] /* 因太多非法 utf8 转义所以不测试该文件了 */
    [InlineData("Assets/Lua/TranslatorTests/utf8.lua")]
    [InlineData("Assets/Lua/TranslatorTests/vararg.lua")]
    [InlineData("Assets/Lua/TranslatorTests/verybig.lua")]
    [InlineData("Assets/Lua/TranslatorTests/empty.lua")]
    public void ExpectSameCode(string filepath) { /* 检查是否与 luac 编出的字节码一致 */
        filepath                     = CommonTestUtils.GetPath(filepath);
        string           fileText    = File.ReadAllText(filepath);
        LuaState         state       = LuaState.NewState();
        AntlrInputStream inputStream = new AntlrInputStream(fileText);
        LClosure         lclosure    = LuaParserUtils._RawParse(state, inputStream, filepath);

        FileIR fileIR1 = LuacUtils.DecompileByLuac(filepath);
        FileIR fileIR2 = LuacUtils.Decompile(lclosure.proto, filepath);

        string cachepath = CommonTestUtils.GetPath($"out/DecompileCache/{Path.GetFileNameWithoutExtension(filepath)}.cslua");
        File.WriteAllText(cachepath, fileIR2.ToString());

        /* 测试 FileIR 是否一样 */
        fileIR1.AssertSame(fileIR2);
    }
}

}
