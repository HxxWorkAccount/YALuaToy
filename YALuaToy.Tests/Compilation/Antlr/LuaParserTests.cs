namespace YALuaToy.Tests.Compilation.Antlr {

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

public class LuaParserTests
{
    private readonly ITestOutputHelper _output;
    public LuaParserTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Theory]
    [InlineData("Assets/Lua/OfficialTests/all.lua")]
    [InlineData("Assets/Lua/OfficialTests/api.lua")]
    [InlineData("Assets/Lua/OfficialTests/attrib.lua")]
    [InlineData("Assets/Lua/OfficialTests/big.lua")]
    [InlineData("Assets/Lua/OfficialTests/bitwise.lua")]
    [InlineData("Assets/Lua/OfficialTests/bwcoercion.lua")]
    [InlineData("Assets/Lua/OfficialTests/calls.lua")]
    [InlineData("Assets/Lua/OfficialTests/closure.lua")]
    [InlineData("Assets/Lua/OfficialTests/code.lua")]
    [InlineData("Assets/Lua/OfficialTests/constructs.lua")]
    [InlineData("Assets/Lua/OfficialTests/coroutine.lua")]
    [InlineData("Assets/Lua/OfficialTests/db.lua")]
    [InlineData("Assets/Lua/OfficialTests/errors.lua")]
    [InlineData("Assets/Lua/OfficialTests/events.lua")]
    [InlineData("Assets/Lua/OfficialTests/files.lua")]
    [InlineData("Assets/Lua/OfficialTests/gc.lua")]
    [InlineData("Assets/Lua/OfficialTests/goto.lua")]
    [InlineData("Assets/Lua/OfficialTests/heavy.lua")]
    [InlineData("Assets/Lua/OfficialTests/literals.lua")]
    [InlineData("Assets/Lua/OfficialTests/locals.lua")]
    [InlineData("Assets/Lua/OfficialTests/main.lua")]
    [InlineData("Assets/Lua/OfficialTests/math.lua")]
    [InlineData("Assets/Lua/OfficialTests/nextvar.lua")]
    [InlineData("Assets/Lua/OfficialTests/pm.lua")]
    [InlineData("Assets/Lua/OfficialTests/sort.lua")]
    [InlineData("Assets/Lua/OfficialTests/strings.lua")]
    [InlineData("Assets/Lua/OfficialTests/tpack.lua")]
    [InlineData("Assets/Lua/OfficialTests/utf8.lua")]
    [InlineData("Assets/Lua/OfficialTests/vararg.lua")]
    [InlineData("Assets/Lua/OfficialTests/verybig.lua")]
    public void LuaParser_MiscValidTests(string filepath) { /* 检测是否报错 */
        filepath                      = Path.Join(CommonTestUtils.CWD(), filepath);
        string           fileText     = File.ReadAllText(filepath);
        AntlrInputStream inputStream  = new AntlrInputStream(fileText);
        LuaLexer         lexer        = new LuaLexer(inputStream);
        lexer.TokenFactory            = new LTokenFactory();
        CommonTokenStream tokenStream = new CommonTokenStream(lexer);
        LuaParser         parser      = new LuaParser(tokenStream);

        parser.start();
    }
}

}
