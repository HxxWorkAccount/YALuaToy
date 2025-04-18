namespace YALuaToy.Core {
using System;
using YALuaToy.Const;
using YALuaToy.Debug;

internal struct LuaType
{
    private sbyte _type;

    public LuaType(sbyte type) {
        _type = type;
    }

    public static implicit operator LuaType(sbyte value) {
        return new LuaType(value);
    }

    /* 直接比较建议用 NotNone 或 Raw，也可以直接用下面的 Check 方法。Tag 和 Variant 的性能一般因为有分支 */
    public sbyte Tag     => _type == LuaConst.TNONE ? LuaConst.TNONE : LuaConst.TagPart(_type);
    public sbyte Variant => _type == LuaConst.TNONE ? LuaConst.TNONE : LuaConst.VariantPart(_type);
    public sbyte NotNoneTag => LuaConst.TagPart(_type);
    public sbyte NotNoneVariant => LuaConst.VariantPart(_type);
    public sbyte Raw     => _type;

    public bool IsClosure {
        get { return Variant == LuaConst.TLCL || Variant == LuaConst.TCCL; }
    }
    public bool IsLuaObject {
        get { return (_type & LuaConst.BIT_ISLUAOBJECT) != 0; }
    }

    public override string ToString() {
        return $"{LuaConst.TypeName(Variant)}({_type})";
    }

    /* ---------------- Utils ---------------- */

    public static bool CheckTag(in LuaValue luaValue, params LuaType[] tags) {
        return CheckTag(luaValue.Type, tags);
    }
    public static bool CheckTag(LuaType luaType, params LuaType[] tags) {
        foreach (LuaType tag in tags) {
            if (luaType.Raw == LuaConst.TNONE && tag.Raw == LuaConst.TNONE)
                return true;
            if (luaType.NotNoneTag == tag.NotNoneTag)
                return true;
        }
        return false;
    }
    public static bool CheckVariant(in LuaValue luaValue, params LuaType[] variants) {
        return CheckVariant(luaValue.Type, variants);
    }
    public static bool CheckVariant(LuaType luaType, params LuaType[] variants) {
        foreach (LuaType variant in variants) {
            if (luaType.Raw == LuaConst.TNONE && variant.Raw == LuaConst.TNONE)
                return true;
            if (luaType.NotNoneVariant == variant.NotNoneVariant)
                return true;
        }
        return false;
    }
    public static bool Check(LuaType luaType, Tuple<LuaType> tags, params LuaType[] variants) {
        return CheckTag(luaType, tags.Item1) || CheckVariant(luaType, variants);
    }
    public static bool Check(LuaType luaType, Tuple<LuaType, LuaType> tags, params LuaType[] variants) {
        return CheckTag(luaType, tags.Item1, tags.Item2) || CheckVariant(luaType, variants);
    }
    public static bool Check(LuaType luaType, Tuple<LuaType, LuaType, LuaType> tags, params LuaType[] variants) {
        return CheckTag(luaType, tags.Item1, tags.Item2, tags.Item3) || CheckVariant(luaType, variants);
    }
    public static bool Check(LuaType luaType, Tuple<LuaType, LuaType, LuaType, LuaType> tags, params LuaType[] variants) {
        return CheckTag(luaType, tags.Item1, tags.Item2, tags.Item3, tags.Item4) || CheckVariant(luaType, variants);
    }
    public static bool Check(LuaType luaType, Tuple<LuaType, LuaType, LuaType, LuaType, LuaType> tags, params LuaType[] variants) {
        return CheckTag(luaType, tags.Item1, tags.Item2, tags.Item3, tags.Item4, tags.Item5) || CheckVariant(luaType, variants);
    }
}

}
