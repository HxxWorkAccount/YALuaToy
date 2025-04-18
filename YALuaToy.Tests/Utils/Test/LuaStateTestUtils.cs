namespace YALuaToy.Tests.Utils.Test {

using Xunit;
using YALuaToy.Core;
using YALuaToy.Const;
using YALuaToy.Tests.Utils;

internal static class LuaStateTestUtils
{
    /* 断言栈顶前 n 个元素（参数 elems 最右边的元素是栈顶） */
    public static void TestTopElems(LuaState state, params object[] elems) {
        for (int i = 0; i < elems.Length; i++) {
            LuaValue expected = CommonTestUtils.CreateLuaValue(elems[elems.Length - i - 1]);
            Assert.True(state.ToRawIdx(-i - 1, out RawIdx rawIdx));
            LuaValue curr = state.GetStack(rawIdx);
            // Assert.True(expected.Type.Raw == curr.Type.Raw, $"Raw type not equal. {expected.Type.Raw}, {curr.Type.Raw}.");
            Assert.True(expected.Equals(curr), $"Value not equals: {expected}, {curr}");
        }
    }

    public static void TestValue(LuaState state, int targetLdx, in LuaValue value) {
        Assert.True(state.ToRawIdx(targetLdx, out RawIdx rawIdx));
        LuaValue target = state.GetStack(rawIdx);
        Assert.True(target.Equals(value), $"Two value not equal: {target} != {value}");
    }

    public static void TestTableKeyValue(LuaState state, int tableLdx, object key, object value) {
        int tableALdx = state.ToAbsLdx(tableLdx);
        LuaValue expectedKey = CommonTestUtils.CreateLuaValue(key);
        LuaValue expectedValue = CommonTestUtils.CreateLuaValue(value);
        state.PushStack(expectedKey);
        state.GetTable(tableALdx);
        Assert.True(expectedValue.Equals(state.GetStack(state.Top-1)));
        state.Pop();
    }

    public static void TestArith(LuaState state, Op op, Func<bool> testFunc) {
        /* 假设参数已压入栈 */
        state.Arith(op);
        Assert.True(testFunc());
        state.Pop();
    }
    public static void TestIntArith(LuaState state, Op op, long a, long b, double expected) {
        Action<long, long, double> testFunc = (x, y, res) => Assert.Equal(res, expected);
        TestIntArith(state, op, a, b, testFunc);
    }
    public static void TestIntArith(LuaState state, Op op, long a, long b, Action<long, long, double> assertFunc) {
        state.Push(a);
        if (op != Op.UNM && op != Op.BNOT)
            state.Push(b);
        state.Arith(op);
        double result = state.ToNumber(-1, out bool success);
        Assert.True(success);
        assertFunc(a, b, result);
        state.Pop();
    }
    public static void TestFltArith(LuaState state, Op op, double a, double b, double expected) {
        Action<double, double, double> assertFunc = (x, y, res) => Assert.Equal(res, expected, 5); /* 浮点数五个精度以内算相同 */
        TestFltArith(state, op, a, b, assertFunc);
    }
    public static void TestFltArith(LuaState state, Op op, double a, double b, Action<double, double, double> assertFunc) {
        state.Push(a);
        state.Push(b);
        state.Arith(op);
        double result = state.ToNumber(-1, out bool success);
        Assert.True(success);
        assertFunc(a, b, result);
        state.Pop();
    }
}

}
