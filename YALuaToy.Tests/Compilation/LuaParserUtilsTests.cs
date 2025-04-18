namespace YALuaToy.Tests.Compilation {

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

public class LuaParserUtilsTests
{
    private readonly ITestOutputHelper _output;
    public LuaParserUtilsTests(ITestOutputHelper output) {
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
    // [InlineData("Assets/Lua/TranslatorTests/tpack.lua")]
    [InlineData("Assets/Lua/TranslatorTests/utf8.lua")]
    [InlineData("Assets/Lua/TranslatorTests/vararg.lua")]
    [InlineData("Assets/Lua/TranslatorTests/verybig.lua")]
    public void RawParse_MiscValidTests(string filepath) { /* 检测是否报错 */
        filepath                     = Path.Join(CommonTestUtils.CWD(), filepath);
        string           fileText    = File.ReadAllText(filepath);
        LuaState         state       = LuaState.NewState();
        AntlrInputStream inputStream = new AntlrInputStream(fileText);
        LClosure         lclosure    = LuaParserUtils._RawParse(state, inputStream, filepath);
    }
}

}
