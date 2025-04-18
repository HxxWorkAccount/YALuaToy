namespace YALuaToy.Compilation.Antlr {

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using DFA = Antlr4.Runtime.Dfa.DFA;
using YALuaToy.Core;
using YALuaToy.Debug;
using YALuaToy.Const;
using YALuaToy.Compilation;

public partial class LuaParser
{
    public partial class StatContext
    {
        internal virtual void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            throw new NotSupportedException();
        }
    }
    public partial class GenericForContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateGenericFor(this);
        }
    }
    public partial class GotoContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateGoto(this);
        }
    }
    public partial class GlobalFunctionContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateGlobalFunction(this);
        }
    }
    public partial class NumericForContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateNumericFor(this);
        }
    }
    public partial class BreakContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateBreak(this);
        }
    }
    public partial class LabelStatContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateLabel(label(), last, inRepeat);
        }
    }
    public partial class RepeatContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateRepeat(this);
        }
    }
    public partial class DoContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateDo(this);
        }
    }
    public partial class WhileContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateWhile(this);
        }
    }
    public partial class EmptyStatContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateEmptyStat(this);
        }
    }
    public partial class LocalAttrContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateLocalAttr(this);
        }
    }
    public partial class FunctionCallStatContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateFunctionCallStat(this);
        }
    }
    public partial class LocalFunctionContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateLocalFunction(this);
        }
    }
    public partial class AssignContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateAssign(this);
        }
    }
    public partial class IfContext
    {
        internal override void _Translate(LuaCodeTranslator translator, bool last, bool inRepeat) {
            translator._TranslateIf(this);
        }
    }
}

}
