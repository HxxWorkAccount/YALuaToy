namespace YALuaToy.Tests.Utils.Mock {

using YALuaToy.Core;
using YALuaToy.Const;

public static class LuaStateMockUtils
{
    public static void PushIntegers(LuaState state, int count) {
        for (int i = 1; i <= count; i++)
            state.Push(i);
    }

    public static void SetTable(LuaState state, int tableLdx, object key, object value) {
        int tableALdx = state.ToAbsLdx(tableLdx);
        state.PushStack(CommonTestUtils.CreateLuaValue(key));   /* push key */
        state.PushStack(CommonTestUtils.CreateLuaValue(value)); /* push value */
        state.SetTable(tableALdx);
    }

    public static void CreateTable(LuaState state, params object[] pairs) {
        state.NewTable();
        for (int i = 0; i < pairs.Length - 1; i += 2) {
            state.PushStack(CommonTestUtils.CreateLuaValue(pairs[i]));     /* push key */
            state.PushStack(CommonTestUtils.CreateLuaValue(pairs[i + 1])); /* push value */
            state.SetTable(-3);
        }
    }

    public static void CreateTables(LuaState state, int count, bool metatable = false) {
        /* 创建 n 个表，弱 metatable=true 则从 -1 读取元表 */
        int metatableLdx = state.ToAbsLdx(-1);
        for (int i = 0; i < count; i++) {
            state.NewTable();
            if (metatable) {
                state.PushValue(metatableLdx);
                state.SetMetatable(-2);
            }
        }
        state.Remove(metatableLdx);
    }

    public static void Sweep(LuaState state) {
        int count = state.CurrCI.Top - state.Top;
        for (int i = 0; i < count; i++)
            state.PushNil();
        for (int i = 0; i < count; i++)
            state.Pop();
    }
}

}
