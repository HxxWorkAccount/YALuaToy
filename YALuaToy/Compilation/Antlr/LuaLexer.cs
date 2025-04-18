namespace YALuaToy.Compilation.Antlr {

using System;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using DFA = Antlr4.Runtime.Dfa.DFA;
using YALuaToy.Core;
using System.Net.Http.Headers;

public class LToken : CommonToken
{
    public readonly long   i;
    public readonly double n;
    public string          str; /* 理论上初始化后就不变了，但可能在 Emit 把重复字符串都指向同一个，以节省内存 */

    public LToken(int type, string text): base(type, text) {
        // Console.WriteLine($"Create Token: {type}, '{text}'");
        (i, n, str) = GetValueFromText(type, text);
    }
    public LToken(Tuple<ITokenSource, ICharStream> source, int type, string text, int channel, int start, int stop, int line):
        base(source, type, channel, start, stop) {
        if (text == null && source.Item2 != null)
            text = source.Item2.GetText(Interval.Of(start, stop));
        else
            text = "<no-source>";
        // Console.WriteLine($"Create Token, Line: {line}, {type}, '{text}'");
        (i, n, str) = GetValueFromText(type, text);
    }

    public string SourceString {
        get {
            if (LuaLexerUtils.IsIntToken(Type))
                return i.ToString();
            else if (LuaLexerUtils.IsFloatToken(Type))
                return n.ToString();
            return str;
        }
    }
    public string    TypeName => LuaLexerUtils.GetTypeName(Type);
    internal UnaryOp UnaryOp {
        get {
            switch (Type) {
            case LuaLexer.NOT:
                return UnaryOp.NOT;
            case LuaLexer.MINUS:
                return UnaryOp.MINUS;
            case LuaLexer.SQUIG:
                return UnaryOp.BNOT;
            case LuaLexer.POUND:
                return UnaryOp.LEN;
            default:
                return UnaryOp.NOUNOPR;
            }
        }
    }
    internal BinaryOp BinaryOp {
        get {
            switch (Type) {
            case LuaLexer.PLUS:
                return BinaryOp.ADD;
            case LuaLexer.MINUS:
                return BinaryOp.SUB;
            case LuaLexer.STAR:
                return BinaryOp.MUL;
            case LuaLexer.SLASH:
                return BinaryOp.DIV;
            case LuaLexer.PER:
                return BinaryOp.MOD;
            case LuaLexer.CARET:
                return BinaryOp.POW;
            case LuaLexer.IDIV:
                return BinaryOp.IDIV;
            case LuaLexer.AMP:
                return BinaryOp.BAND;
            case LuaLexer.PIPE:
                return BinaryOp.BOR;
            case LuaLexer.SQUIG:
                return BinaryOp.BXOR;
            case LuaLexer.SHL:
                return BinaryOp.SHL;
            case LuaLexer.SHR:
                return BinaryOp.SHR;
            case LuaLexer.CONCAT:
                return BinaryOp.CONCAT;
            case LuaLexer.NE:
                return BinaryOp.NE;
            case LuaLexer.EQ:
                return BinaryOp.EQ;
            case LuaLexer.LT:
                return BinaryOp.LT;
            case LuaLexer.LE:
                return BinaryOp.LE;
            case LuaLexer.GT:
                return BinaryOp.GT;
            case LuaLexer.GE:
                return BinaryOp.GE;
            case LuaLexer.AND:
                return BinaryOp.AND;
            case LuaLexer.OR:
                return BinaryOp.OR;
            default:
                return BinaryOp.NOBINOPR;
            }
        }
    }

    internal static (long i, double n, string str) GetValueFromText(int type, string text) {
        int consumedCharCount;
        switch (type) {
        case LuaLexer.Eof:
            return (0, 0, LuaLexerUtils.GetTypeName(LuaLexer.Eof));
        case LuaLexer.FLOAT:
        case LuaLexer.HEX_FLOAT:
        case LuaLexer.INT:
        case LuaLexer.HEX:
            consumedCharCount = LuaUtils.StringToLuaNumber(text, out LuaValue value);
            LuaLexerUtils.LexerCheck(consumedCharCount > 0, $"Invalid number: {text}", type);
            if (value.IsInt)
                return (value.Int, 0, null);
            else
                return (0, value.Number, null);
        case LuaLexer.CHARSTRING:
        case LuaLexer.NORMALSTRING:
            return (0, 0, LuaLexerUtils.ReadShortString(text, type));
        case LuaLexer.LONGSTRING:
            return (0, 0, LuaLexerUtils.ReadLongString(text));
        default:
            return (0, 0, text);
        }
    }
}

public class LTokenFactory : CommonTokenFactory
{
    public override CommonToken
    Create(Tuple<ITokenSource, ICharStream> source, int type, string text, int channel, int start, int stop, int line, int charPositionInLine) {
        return new LToken(source, type, text, channel, start, stop, line);
    }
    public override CommonToken Create(int type, string text) {
        return new LToken(type, text);
    }
}

/* 只用于提供语义动作 */
public partial class LuaLexer
{
    internal readonly LuaTable constantsIMap;
    internal const int         reservedWordCount = DBCOLON;

    internal LuaLexer(ICharStream input, LuaTable constantsIMap): this(input) {
        this.constantsIMap = constantsIMap;
    }

    internal LuaLexer(ICharStream input, TextWriter output, TextWriter errorOutput, LuaTable constants): this(input, output, errorOutput) {
        this.constantsIMap = constants;
    }

    public bool IsLine1Col0() {
        ICharStream cs = (ICharStream)InputStream;
        if (cs.Index == 1)
            return true;
        return false;
    }

    public override IToken Emit() {
        IToken token = base.Emit();
        if (token != null && LuaLexerUtils.IsStringToken(token.Type)) {
            LToken lToken = (LToken)token;
            lToken.str    = AddStringConstants(lToken); /* 确保用同一份 str */
        }
        return token;
    }

    private string AddStringConstants(IToken token) {
        LToken ltoken = (LToken)token;
        LuaLexerUtils.LexerCheck(LuaLexerUtils.IsStringToken(token.Type));
        if (constantsIMap == null)
            return ltoken.str;
        LuaValue stringValue     = new LuaValue(ltoken.str);
        LuaValue prevStringValue = constantsIMap.Get(stringValue);
        if (prevStringValue.IsNil) {
            constantsIMap.Set(stringValue, stringValue);
            return stringValue.Str;
        }
        return prevStringValue.Str;
    }
}
}
