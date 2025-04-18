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

public class LuaLexerTests
{
    private readonly ITestOutputHelper _output;
    public LuaLexerTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Theory]
    [InlineData("Assets/Lua/LexerTests/misc.lua")]
    [InlineData("Assets/Lua/LexerTests/all.lua")]
    [InlineData("Assets/Lua/LexerTests/api.lua")]
    [InlineData("Assets/Lua/LexerTests/attrib.lua")]
    [InlineData("Assets/Lua/LexerTests/big.lua")]
    [InlineData("Assets/Lua/LexerTests/bitwise.lua")]
    [InlineData("Assets/Lua/LexerTests/bwcoercion.lua")]
    [InlineData("Assets/Lua/LexerTests/calls.lua")]
    [InlineData("Assets/Lua/LexerTests/closure.lua")]
    [InlineData("Assets/Lua/LexerTests/code.lua")]
    [InlineData("Assets/Lua/LexerTests/constructs.lua")]
    [InlineData("Assets/Lua/LexerTests/coroutine.lua")]
    [InlineData("Assets/Lua/LexerTests/db.lua")]
    [InlineData("Assets/Lua/LexerTests/errors.lua")]
    [InlineData("Assets/Lua/LexerTests/events.lua")]
    [InlineData("Assets/Lua/LexerTests/gc.lua")]
    [InlineData("Assets/Lua/LexerTests/goto.lua")]
    [InlineData("Assets/Lua/LexerTests/heavy.lua")]
    [InlineData("Assets/Lua/LexerTests/literals.lua")]
    [InlineData("Assets/Lua/LexerTests/locals.lua")]
    [InlineData("Assets/Lua/LexerTests/main.lua")]
    [InlineData("Assets/Lua/LexerTests/math.lua")]
    [InlineData("Assets/Lua/LexerTests/nextvar.lua")]
    [InlineData("Assets/Lua/LexerTests/pm.lua")]
    [InlineData("Assets/Lua/LexerTests/sort.lua")]
    [InlineData("Assets/Lua/LexerTests/strings.lua")]
    [InlineData("Assets/Lua/LexerTests/tpack.lua")]
    [InlineData("Assets/Lua/LexerTests/utf8.lua")]
    [InlineData("Assets/Lua/LexerTests/vararg.lua")]
    [InlineData("Assets/Lua/LexerTests/verybig.lua")]
    public void LuaLexer_Expected_Same(string filepath) {
        StringReader cluaDumpResult = Utils.GetCLuaDumpResult(filepath);
        StringReader csLexerResult  = Utils.GetLuaLexerResult(filepath);

        int GetLineNum(string? line) {
            Assert.NotNull(line);
            Match match = Regex.Match(line, @"^Line\s+(\d+),");
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
            else
                Assert.Fail($"Recover failed, line: {line}");
            return 0;
        }

        string ? cluaDumpLine;
        string ? csluaLexerLine;

        void Recover() {
            /* 不一样时，跳转到下一行（如果行数不一样，则小的那方跳转到大的那行） */
            int cluaLine   = GetLineNum(cluaDumpLine);
            int csluaLine  = GetLineNum(csluaLexerLine);
            int targetLine = cluaLine == csluaLine ? cluaLine + 1 : Math.Max(cluaLine, csluaLine);
            while (cluaLine < targetLine)
                cluaLine = GetLineNum(cluaDumpLine = cluaDumpResult.ReadLine());
            while (csluaLine < targetLine)
                csluaLine = GetLineNum(csluaLexerLine = csLexerResult.ReadLine());
        }

        bool failed = false;
        while ((cluaDumpLine = cluaDumpResult.ReadLine()) != null) {
            csluaLexerLine = csLexerResult.ReadLine();
        loopStart:
            if (csluaLexerLine == null)
                break;
            // Console.WriteLine($"clua:  {cluaDumpLine.Trim()}\ncslua: {csluaLexerLine.Trim()}");
            if (csluaLexerLine.Trim() == cluaDumpLine.Trim()) {
                if (!failed && cluaDumpLine.EndsWith("'<eof>'")) {
                    Console.WriteLine("Success!");
                    return;
                }
                continue;
            } else {
                string msg = "Two lexer results are not the same:";
                Console.WriteLine($"\u001b[31m{msg}\u001b[0m\n\tclua:  {cluaDumpLine}\n\tcslua: {csluaLexerLine}");
                Recover();
                failed = true;
                goto loopStart;
            }
        }
        Assert.Fail("The two lexer results are not the same.");
    }
    internal class Utils
    {
        internal static StringReader GetCLuaDumpResult(string filepath) {
            filepath             = Path.Join(CommonTestUtils.CWD(), filepath);
            string dumplexer     = "CLua/build/Debug/bin/dumplexer";
            dumplexer            = Path.Join(CommonTestUtils.CWD(), dumplexer);
            ProcessStartInfo psi = new ProcessStartInfo(dumplexer, filepath) {
                /* 用 UTF8 读取输出 */
                RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8, CreateNoWindow = true
            };

            string output = "";
            using (Process process = new Process { StartInfo = psi }) {
                process.Start();

                // 读取所有输出（你也可以使用 ReadLine() 按行读取）
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }
            StringReader sr = new StringReader(output);
            sr.ReadLine(); /* skip the first line */
            return sr;
        }

        internal static StringReader GetLuaLexerResult(string filepath) {
            filepath                      = Path.Join(CommonTestUtils.CWD(), filepath);
            string           fileText     = File.ReadAllText(filepath);
            AntlrInputStream inputStream  = new AntlrInputStream(fileText);
            var              lexer        = new LuaLexer(inputStream);
            lexer.TokenFactory            = new LTokenFactory();
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill();

            StringBuilder sb = new StringBuilder();
            foreach (var token in tokenStream.GetTokens()) {
                if (token.Type == LuaLexer.COMMENT)
                    continue;
                if (token.Type == LuaLexer.WS)
                    continue;
                LToken ltoken = (LToken)token;
                if (LuaLexerUtils.IsFloatToken(token.Type) || ltoken.n > 0) /* Token 是整型但值也有可能是浮点数（超大） */
                    continue;
                /* 格式化输出：行号-Token类型-源内容 */
                if (ltoken.TypeName == "<noname>")
                    continue;
                sb.AppendLine($"Line {ltoken.Line}, {ltoken.TypeName}, '{ltoken.SourceString}'");
            }
            return new StringReader(sb.ToString());
        }
    }
}

}
