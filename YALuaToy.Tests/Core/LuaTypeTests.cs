namespace YALuaToy.Tests.Core {

using System;
using Xunit;
using YALuaToy.Core;
using YALuaToy.Const;
using YALuaToy.Debug;

public class LuaTypeTests
{
    [Theory]
    [InlineData(LuaConst.TNONE, true, LuaConst.TNONE, true)]
    [InlineData(-1, false, LuaConst.TNONE, true)]
    [InlineData(LuaConst.TNIL, false, LuaConst.TNONE, false)]
    [InlineData(LuaConst.TTHREAD, true, LuaConst.TNONE, false)]
    [InlineData(LuaConst.TNUMINT, false, LuaConst.TNONE, false)]
    [InlineData(LuaConst.TNUMINT, false, LuaConst.TNUMBER, true)]
    [InlineData(LuaConst.TLCF, false, LuaConst.TFUNCTION, true)]
    [InlineData(LuaConst.TCCL, true, LuaConst.TFUNCTION, true)]
    [InlineData(LuaConst.TTHREAD, true, LuaConst.TTHREAD, true)]
    public void CheckTag_MatchingType_Expected(sbyte type, bool mark, sbyte target, bool expected) {
        _Check_MatchingType_Expected(type, mark, target, expected, true);
    }

    [Theory]
    [InlineData(LuaConst.TNUMINT, false, LuaConst.TNUMFLT, false)]
    [InlineData(LuaConst.TCCL, true, LuaConst.TLCL, false)]
    public void CheckVariant_MatchingType_Expected(sbyte type, bool mark, sbyte target, bool expected) {
        _Check_MatchingType_Expected(type, mark, target, expected, false);
    }

    private void _Check_MatchingType_Expected(sbyte type, bool mark, sbyte target, bool matched, bool isTag) {
        LuaType targetType = new LuaType(target);
        LuaType luaType;
        if (mark)
            luaType = new LuaType(LuaConst.MarkLuaObject(type));
        else
            luaType = new LuaType(type);

        if (isTag)
            Assert.True(
                LuaType.CheckTag(luaType, targetType) == matched,
                $"Expected check tag '{luaType}' and target '{targetType}' with result: '{matched}', but was '{!matched}'."
            );
        else
            Assert.True(
                LuaType.CheckVariant(luaType, targetType) == matched,
                $"Expected check variant '{luaType}' and target '{targetType}' with result: '{matched}', but was '{!matched}'."
            );
    }
}

}
