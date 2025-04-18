namespace YALuaToy.Core {

using System;
using System.Collections.Generic;
using YALuaToy.Const;
using YALuaToy.Debug;

internal partial class LuaGlobalState
{
    public LuaValue FastGetTagMethod(LuaTable metatable, TagMethod tag) {
        if (metatable == null)
            return LuaValue.NONE;
        if (metatable.GetFlags(tag))
            return LuaValue.NONE;
        else
            return metatable._FastGetTagMethod(tag, TagMethodNames[(int)tag]);
    }

    public LuaTable GetMetatable(in LuaValue luaValue) {
        switch (luaValue.Type.NotNoneTag) {
        case LuaConst.TTABLE:
            return luaValue.LObject<LuaTable>().Metatable;
        case LuaConst.TUSERDATA:
            return luaValue.LObject<LuaUserData>().Metatable;
        }
        return MetaTables[luaValue.Type.NotNoneTag];
    }
}

public partial class LuaState
{
    internal LuaValue FastGetTagMethod(LuaTable metatable, TagMethod tag) {
        return globalState.FastGetTagMethod(metatable, tag);
    }

    internal LuaValue GetTagMethod(in LuaValue luaValue, TagMethod tag) {
        LuaTable metatable = globalState.GetMetatable(luaValue);
        if (metatable != null)
            return metatable.Get(new LuaValue(globalState.TagMethodNames[(int)tag]));
        else
            return LuaValue.NIL;
    }

    /* 返回对象名，会考虑元方法 __name */
    internal string TypeName(in LuaValue luaValue) {
        if (LuaType.CheckTag(luaValue.Type, LuaConst.TTABLE, LuaConst.TUSERDATA)) {
            LuaTable metatable = globalState.GetMetatable(luaValue);
            if (metatable != null) {
                LuaValue name = metatable.Get(new LuaValue(LuaGlobalState.tmName));
                if (name.IsString)
                    return name.Str;
            }
        }
        return LuaConst.TypeName(luaValue.Type.Variant);
    }

    /* 该版本任由 tagMethod 在栈上压入值，对于 caller 来说可能改变 top */
    internal void CallTagMethod(in LuaValue tagMethod, in LuaValue arg1, in LuaValue arg2) {
        CallTagMethod(tagMethod, arg1, arg2, LuaValue.NONE);
    }
    internal void CallTagMethod(in LuaValue tagMethod, in LuaValue arg1, in LuaValue arg2, in LuaValue arg3) {
        RawIdx func           = _top;
        _stack[(int)func]     = tagMethod;
        _stack[(int)func + 1] = arg1;
        _stack[(int)func + 2] = arg2;
        _top += 3;
        if (!arg3.Null)
            _stack[(int)_top++] = arg3;
        _Call(func, 1, !_currCI.IsLua); /* 这里只处理返回值为 1 的元方法，比如 __call 啥的不在这处理（参考 _GetFuncTM） */
    }
    internal void CallTagMethodWithoutResult(in LuaValue tagMethod, in LuaValue arg1, in LuaValue arg2, in LuaValue arg3) {
        CallTagMethod(tagMethod, arg1, arg2, arg3);
        _top--;
    }
    /* 把唯一返回值以其他形式返回，对于 caller 来说 top 不变 */
    internal void CallTagMethodWithResult(in LuaValue tagMethod, in LuaValue arg1, in LuaValue arg2, RawIdx output) {
        CallTagMethod(tagMethod, arg1, arg2); /* Call 后 _top 会被 _MoveResults 调整 */
        _top--;
        _stack[(int)output] = _stack[(int)_top];
    }
    /* 把唯一返回值以其他形式返回，对于 caller 来说 top 不变 */
    internal LuaValue CallTagMethodWithResult(in LuaValue tagMethod, in LuaValue arg1, in LuaValue arg2) {
        CallTagMethod(tagMethod, arg1, arg2); /* Call 后 _top 会被 _MoveResults 调整 */
        _top--;
        return _stack[(int)_top];
    }
    internal bool TryBinTagMethod(in LuaValue arg1, in LuaValue arg2, RawIdx output, TagMethod tag) {
        /* 注意命名和 CLua 相反，这里 Try 表示不报错且返回是否成功，而 CLua 里这个函数以 'call' 开头 */
        LuaValue tagMethod = GetTagMethod(arg1, tag);
        if (tagMethod.IsNil)
            tagMethod = GetTagMethod(arg2, tag);
        if (tagMethod.IsNil)
            return false;
        CallTagMethodWithResult(tagMethod, arg1, arg2, output);
        return true;
    }
    internal void CallBinTagMethod(in LuaValue arg1, in LuaValue arg2, RawIdx output, TagMethod tag) {
        if (!TryBinTagMethod(arg1, arg2, output, tag)) {
            switch (tag) {
            case TagMethod.CONCAT:
                throw new LuaConcateError(arg1, arg2);
            case TagMethod.BAND:
            case TagMethod.BOR:
            case TagMethod.BXOR:
            case TagMethod.SHL:
            case TagMethod.SHR:
            case TagMethod.BNOT:
                if (arg1.ToNumber(out _) && arg2.ToNumber(out _))
                    throw new LuaToIntError(arg1, arg2);
                else
                    throw new LuaIntOperationError(arg1, arg2, "perform bitwise operation");
            default:
                throw new LuaIntOperationError(arg1, arg2, "perform arithmetic");
            }
        }
    }
    internal bool TryOrderTagMethod(in LuaValue arg1, in LuaValue arg2, TagMethod tag, out bool result) {
        if (TryBinTagMethod(arg1, arg2, _top, tag)) {
            result = _stack[(int)_top].ToBoolean();
            return true;
        } else {
            result = false;
            return false;
        }
    }
}

internal partial class LuaTable
{
    internal LuaValue _FastGetTagMethod(TagMethod tag, LuaString tagName) {
        LuaValue tagMethod = Get(new LuaValue(tagName));
        if (tagMethod.IsNil) {
            _SetFlags(tag);
            return LuaValue.NONE;
        }
        return tagMethod;
    }
}

}
