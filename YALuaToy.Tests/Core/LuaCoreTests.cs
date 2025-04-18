namespace YALuaToy.Tests.Core {

using System;
using Xunit;
using Xunit.Abstractions;
using YALuaToy.Core;
using YALuaToy.Const;
using YALuaToy.Debug;
using YALuaToy.Tests.Utils;
using YALuaToy.Tests.Utils.Mock;
using YALuaToy.Tests.Utils.Test;
using System.Diagnostics;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Text;
using System.ComponentModel;

public class LuaCoreTests
{
    private readonly ITestOutputHelper _output;
    public LuaCoreTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Fact]
    public void TopLdx_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        state.TopLdx = 6;
        LuaStateTestUtils.TestTopElems(state, 1, 2, 3, 4, 5, CommonTestUtils.NIL);
        Assert.Equal(6, state.TopLdx);

        state.TopLdx = 3;
        LuaStateTestUtils.TestTopElems(state, 1, 2, 3);
        Assert.Equal(3, state.TopLdx);

        state.TopLdx = -2;
        LuaStateTestUtils.TestTopElems(state, 1, 2);
        Assert.Equal(2, state.TopLdx);
    }

    [Fact]
    public void PushValue_Case_1() {
        LuaState state = LuaState.NewState();

        state.Push(true);
        state.Push("haha%d", 10010);
        state.Push(10);
        state.Push(3.14);

        state.PushValue(2);

        Assert.Equal("haha10010", state.ToString(-1, out bool success));
        Assert.True(success);
        Assert.Equal("haha10010", state.ToString(5, out success));
        Assert.True(success);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 5)]
    [InlineData(1, 0)]
    [InlineData(3, -3)]
    public void Rotate_RotateArg_Expected(int ldx, int n) {
        LuaState state = LuaState.NewState();

        int count = 5;

        for (int i = 1; i <= count; i++)
            state.Push(i);

        state.Rotate(ldx, n);

        // 1 2 3 4 5
        int rotateLength = count - ldx + 1;
        if (n < 0)
            n %= rotateLength;
        for (int i = 0; i < rotateLength; i++) {
            int  realLdx       = (i + n) % rotateLength + ldx;
            long expectedValue = ldx + i;
            // i: 0, ldx: 2, n: 2, expectedValue: 2, currValue: 0, rotateLength: 4, realLdx: 4, expectedValue: 2
            long currValue = state.ToInteger(realLdx, out bool success);
            Assert.True(
                expectedValue == currValue,
                $"i: {i}, ldx: {ldx}, n: {n}, expectedValue: {expectedValue}, currValue: {currValue}, rotateLength: {rotateLength}, realLdx: {realLdx}"
            );
            Assert.True(success);
        }
    }

    [Fact]
    public void Copy_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        state.Copy(3, 4);

        LuaStateTestUtils.TestTopElems(state, 1, 2, 3, 3, 5);
    }

    [Fact]
    public void XMove_Case_1() {
        LuaState state1 = LuaState.NewState();
        LuaState state2 = new LuaState(state1);
        LuaStateMockUtils.PushIntegers(state1, 5);

        state1.XMove(state2, 3);

        LuaStateTestUtils.TestTopElems(state1, 1, 2);
        LuaStateTestUtils.TestTopElems(state2, 3, 4, 5);
    }

    [Fact]
    public void Pop_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        state.Pop(2);

        LuaStateTestUtils.TestTopElems(state, 1, 2, 3);
    }

    [Fact]
    public void Insert_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        // Lua 的 lua_insert 操作会将原来位于索引 2 上及以上的元素依次上移，
        // 结果：原栈 [1,2,3,4,5] 变为 [1,5,2,3,4]
        state.Insert(2);

        LuaStateTestUtils.TestTopElems(state, 1, 5, 2, 3, 4);
    }

    [Fact]
    public void Remove_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        // 删除索引 3 的元素：
        // 原栈 [1,2,3,4,5] 删除位置 3（即数字 3）后，应变为 [1,2,4,5]
        state.Remove(3);

        LuaStateTestUtils.TestTopElems(state, 1, 2, 4, 5);
    }

    [Fact]
    public void Replace_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        // 执行 Replace(2)：将栈顶（5）复制到索引 2 的位置，然后弹出栈顶
        // 原栈 [1,2,3,4,5] 经替换后应变为 [1,5,3,4]
        state.Replace(2);

        LuaStateTestUtils.TestTopElems(state, 1, 5, 3, 4);
    }

    [Fact]
    public void ToAbsLdx_Case_1() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.PushIntegers(state, 5);

        Assert.Equal(1, state.ToAbsLdx(1));
        Assert.Equal(3, state.ToAbsLdx(3));
        Assert.Equal(5, state.ToAbsLdx(-1));
        Assert.Equal(3, state.ToAbsLdx(-3));
    }

    [Fact]
    public void LuaCheckStack_Case_1() {
        LuaState state = LuaState.NewState();

        /* 检查额外需要10个槽是否能成功 */
        bool result = state.LuaCheckStack(10);
        Assert.True(result);

        /* 进一步压入一些数据，再次检查较多空间申请 */
        result = state.LuaCheckStack(30);
        LuaStateMockUtils.PushIntegers(state, 30);
        Assert.True(result);
    }

    [Fact]
    public void GetType_MiscCase() {
        LuaState state   = LuaState.NewState();
        int      counter = 0;

        state.LuaCheckStack(20);

        state.PushNil();
        Assert.Equal(LuaConst.TNIL, state.GetType(++counter));

        state.Push(true);
        Assert.Equal(LuaConst.TBOOLEAN, state.GetType(++counter));

        state.Push(123);
        Assert.Equal(LuaConst.TNUMBER, state.GetType(++counter));

        state.Push(3.14159);
        Assert.Equal(LuaConst.TNUMBER, state.GetType(++counter));

        state.Push("hello");
        Assert.Equal(LuaConst.TSTRING, state.GetType(++counter));

        state.Push("hello%s%c%d%I%f", "s", 6, 2000, 3.14159);
        Assert.Equal(LuaConst.TSTRING, state.GetType(++counter));

        state.NewTable();
        Assert.Equal(LuaConst.TTABLE, state.GetType(++counter));

        state.PushGlobalTable();
        Assert.Equal(LuaConst.TTABLE, state.GetType(++counter));

        LuaCFunction dummyFunc = s => 0;
        state.Push(dummyFunc);
        Assert.Equal(LuaConst.TFUNCTION, state.GetType(++counter));

        state.Push(1);
        state.Push(dummyFunc, 1);
        Assert.Equal(LuaConst.TFUNCTION, state.GetType(++counter));

        state.PushSelf();
        Assert.Equal(LuaConst.TTHREAD, state.GetType(++counter));

        LuaState thread = new LuaState(state); /* 该操作会往 state 里压入 thread */
        state.Pop();                           /* 弹出 thread */
        state.XMove(thread, 1);                /* 压入 self */
        counter--;
        Assert.Equal(LuaConst.TTHREAD, thread.GetType(1));

        state.Push(IntPtr.Zero);
        Assert.Equal(LuaConst.TLIGHTUSERDATA, state.GetType(++counter));

        Assert.Equal(LuaConst.TNONE, state.GetType(1000));
    }

    [Fact]
    public void IsNumber_MiscCase() {
        LuaState state = LuaState.NewState();

        state.Push("123.456");
        Assert.True(state.IsNumber(-1));

        state.Push(789.1);
        Assert.True(state.IsNumber(-1));

        state.Push(789);
        Assert.True(state.IsNumber(-1));
    }

    [Fact]
    public void IsString_MiscCase() {
        LuaState state = LuaState.NewState();

        state.Push("hello world");
        Assert.True(state.IsString(-1));

        state.Push(42);
        Assert.True(state.IsString(-1));
    }

    [Fact]
    public void IsCFunction_MiscCase() {
        LuaState state = LuaState.NewState();

        LuaCFunction dummyFunc = s => 0;
        state.Push(dummyFunc);
        Assert.True(state.IsCFunction(-1));
    }

    [Fact]
    public void IsInteger_MiscCase() {
        LuaState state = LuaState.NewState();

        state.Push(123);
        Assert.True(state.IsInteger(-1));
    }

    [Fact]
    public void IsUserData_MiscCase() {
        LuaState state = LuaState.NewState();

        state.Push(IntPtr.Zero);
        Assert.True(state.IsUserData(-1));
    }

    [Fact]
    public void IsFunction_MiscCase() {
        LuaState state = LuaState.NewState();

        LuaCFunction dummyFunc = s => 0;
        state.Push(dummyFunc);
        Assert.True(state.IsFunction(-1));
    }

    [Fact]
    public void IsTable_MiscCase() {
        LuaState state = LuaState.NewState();

        state.NewTable();
        Assert.True(state.IsTable(-1));
    }

    [Fact]
    public void IsLightUserData_MiscCase() {
        LuaState state = LuaState.NewState();

        state.Push(IntPtr.Zero);
        Assert.True(state.IsLightUserData(-1));
    }

    [Fact]
    public void IsNil_MiscCase() {
        LuaState state = LuaState.NewState();

        state.PushNil();
        Assert.True(state.IsNil(-1));
    }

    [Fact]
    public void IsBoolean_MiscCase() {
        LuaState state = LuaState.NewState();

        state.Push(true);
        Assert.True(state.IsBoolean(-1));
        state.Push(false);
        Assert.True(state.IsBoolean(-1));
    }

    [Fact]
    public void IsThread_MiscCase() {
        LuaState state = LuaState.NewState();

        state.PushSelf();
        Assert.True(state.IsThread(-1));
    }

    [Fact]
    public void IsNone_MiscCase() {
        LuaState state = LuaState.NewState();

        // 非法的索引返回 None
        Assert.True(state.IsNone(1000));
    }

    [Fact]
    public void IsNoneOrNil_MiscCase() {
        LuaState state = LuaState.NewState();

        state.PushNil();
        Assert.True(state.IsNoneOrNil(-1));
        Assert.True(state.IsNoneOrNil(1000));
    }

    [Fact]
    public void Register_Case_1() {
        LuaState     state     = LuaState.NewState();
        LuaCFunction dummyFunc = s => 42;
        state.Register("dummyFunc", dummyFunc);
        sbyte tag = state.GetGlobal("dummyFunc");
        Assert.Equal(LuaConst.TFUNCTION, tag);
        LuaCFunction retFunc = state.ToCFunction(-1); /* 检查通过 ToCFunction 获取的函数是否与 dummyFunc 相同 */
        Assert.Equal(dummyFunc, retFunc);
    }

    [Fact]
    public void GetType_Case_PseudoIndex() {
        LuaState state = LuaState.NewState();

        /* REGISTRYINDEX 为伪索引，_Get 中处理该值返回全局注册表 */
        sbyte typeTag = state.GetType(LuaConst.REGISTRYINDEX);

        Assert.True(LuaConst.TTABLE == typeTag, "伪索引 REGISTRYINDEX 未返回全局注册表。");
    }

    [Fact]
    public void GetGlobal_Case_1() {
        LuaState state = LuaState.NewState();
        state.Push(12345);
        state.SetGlobal("globalNumber");
        sbyte tag = state.GetGlobal("globalNumber");
        Assert.Equal(LuaConst.TNUMBER, tag);
        long num = state.ToInteger(-1);
        Assert.Equal(12345, num);
    }

    [Fact]
    public void GetTable_StringKey_Case() {
        LuaState state = LuaState.NewState();
        // 新建一个表，并设置字段 "foo" = "bar"
        state.NewTable();           // table pushed onto stack
        state.Push("bar");          // push value "bar"
        state.SetTable(-2, "foo");  // table["foo"] = "bar" (pops value)
        // 使用 GetTable 获取字段 "foo"
        sbyte tag = state.GetTable(-1, "foo");  // push retrieved value onto stack
        Assert.Equal(LuaConst.TSTRING, tag);
        bool   success;
        string result = state.ToString(-1, out success);
        Assert.True(success);
        Assert.Equal("bar", result);
        state.Pop(2);  // 清除 table 与取出的值
    }

    [Fact]
    public void GetTable_Case_IntegerKey() {
        LuaState state = LuaState.NewState();
        state.NewTable();
        state.Push("value7");
        state.SetTable(-2, 7); /* table[7] = "value7" */
        /* 使用 GetTable 以整数键读取数据 */
        sbyte tag = state.GetTable(-1, 7);
        Assert.Equal(LuaConst.TSTRING, tag);
        string result = state.ToString(-1, out bool success);
        Assert.True(success);
        Assert.Equal("value7", result);
        state.Pop(2);
    }

    [Fact]
    public void GetTable_Case_NonExistingKey() {
        LuaState state = LuaState.NewState();
        /* 新建空表，尝试读取一个不存在的字段 */
        state.NewTable();
        sbyte tag = state.GetTable(-1, "notPresent");  // 取不存在的键应返回 nil
        Assert.Equal(LuaConst.TNIL, tag);
        state.Pop(1);
    }

    [Fact]
    public void GetTable_Case_WithMetatable() {
        LuaState state = LuaState.NewState();
        state.NewTable();
        state.Push("direct");
        state.SetTable(-2, "normal");  // t["normal"] = "direct"

        /* 新建一个元表，并设置 __index 元方法为一个 C 函数，该函数总返回字符串 "fallback" */
        state.NewTable();  // metatable, pushed onto stack
        LuaCFunction indexFunc = s => {
            s.Push("fallback");
            return 1;
        };
        state.Push(indexFunc);
        state.SetTable(-2, "__index");  // metatable["__index"] = indexFunc
        state.SetMetatable(-2);         // pop metatable, t 仍在栈顶

        /* 尝试读取一个表中不存在的键 "missing"，应触发元方法 __index，返回 "fallback" */
        sbyte tag = state.GetTable(-1, "missing");
        Assert.Equal(LuaConst.TSTRING, tag);
        string result = state.ToString(-1, out bool success);
        Assert.True(success);
        Assert.Equal("fallback", result);
        state.Pop(2);
        Assert.Equal(1, (int)state.Top);
    }

    [Fact]
    public void GetTable_Case_PseudoIndex() {
        LuaState state = LuaState.NewState();

        /* 全局表 */
        state.Push(LuaConst.RIDX_GLOBALS);
        state.GetTable(LuaConst.REGISTRYINDEX);
        LuaValue globalTable = state.globalState.Registry.Get(new LuaValue(LuaConst.RIDX_GLOBALS));
        Assert.True(globalTable.Equals(state.GetStack(state.Top - 1)));
    }

    [Fact]
    public void UpvalueIndex_Case_1() {
        LuaState state = LuaState.NewState();

        state.Push("initial"); /* 先压入一个值作为上值 */

        /* 构造闭包，有 1 个上值 */
        LuaCFunction dummyFunc = s => 0;
        state.Push(dummyFunc, 1);

        /* 获取上值 */
        string upName = state.GetUpvalue(-1, 1);
        Assert.NotNull(upName);
        Assert.Equal("", upName); /* 由于是 CClosure，上值的名字为空字符串 */
        string upvalue = state.ToString(-1, out bool success);
        Assert.True(success);
        Assert.Equal("initial", upvalue);
        state.Pop(1);

        /* 修改上值 */
        state.Push("newValue");
        string setName = state.SetUpvalue(-2, 1);
        Assert.NotNull(setName);
        /* 再次获取上值，验证上值已经更新 */
        upName = state.GetUpvalue(-1, 1);
        Assert.NotNull(upName);
        string newUpvalue = state.ToString(-1, out success);
        Assert.True(success);
        Assert.Equal("newValue", newUpvalue);

        /* 清理栈：弹出上值和闭包 */
        state.Pop(2);
    }

    [Fact]
    public void RawGetTable_MiscCase() {
        LuaState state = LuaState.NewState();

        state.NewTable();

        /* table["edgeKey"] = "edgeValue" */
        state.Push("edgeValue");
        state.SetTable(-2, "edgeKey");

        /* 查找表中存在的键 */
        state.Push("edgeKey");
        sbyte tag = state.RawGetTable(-2);
        Assert.Equal(LuaConst.TSTRING, tag);
        string result = state.ToString(-1, out bool success);
        Assert.True(success);
        Assert.Equal("edgeValue", result);
        state.Pop(1);

        /* 查找表中不存在的键 */
        tag = state.RawGetTable(-1, "nonexistent");
        Assert.Equal(LuaConst.TNIL, tag);
        state.Pop(1); /* 弹出表 */
    }

    [Fact]
    public void SetAndGetMetatable_MiscCase() {
        LuaState state = LuaState.NewState();

        state.NewTable();

        state.NewTable(); /* metatable */
        state.Push("bar");
        state.SetTable(-2, "foo"); /* metatable["foo"] = "bar" */
        LuaCFunction index = (s) => {
            state.Push("no result");
            return 1;
        };
        state.Push("__index");
        state.Push(index);
        state.SetTable(-3); /* metatable["__index"] = index */

        state.SetMetatable(-2);

        /* 读取 metatable */
        bool success = state.GetMetatable(-1);
        Assert.True(success);

        /* 验证 metatable["foo"] */
        sbyte tag = state.GetTable(-1, "foo");  // 从 metatable中取出字段
        Assert.Equal(LuaConst.TSTRING, tag);
        string result = state.ToString(-1, out success);
        Assert.True(success);
        Assert.Equal("bar", result);
        state.Pop();

        /* 验证 metatable["__index"] */
        state.RawGetTable(-1, "__index");
        Assert.True(state.GetStack(state.Top - 1).LightFunc == index);
        state.Pop();
        state.GetTable(-2, "invalid key"); /* 读取表的无效键，看是否能触发 __index */
        string output = state.ToString(-1, out success);
        Assert.True(output == "no result", $"output: {output}, success: {success}");

        /* 清除 metatable["__index"] */
        state.Pop();
        state.PushNil();
        state.RawSetTable(-2, "__index");
        state.GetTable(-2, "invalid key"); /* 读取表的无效键，看是否能触发 __index */
        Assert.True(state.GetType(-1) == LuaConst.TNIL);
    }

    [Fact]
    public void RawSetTable_MiscCase() {
        LuaState state = LuaState.NewState();
        state.NewTable();

        /* 设置元表 __newindex */
        state.NewTable();
        LuaCFunction newIndexFunc = s => {
            s.Pop();
            s.Push("metamethod value");
            s.RawSetTable(-3);
            return 0;
        };
        state.Push(newIndexFunc);
        state.SetTable(-2, "__newindex");
        state.SetMetatable(-2);

        /* 对于普通 SetTable（非 raw），若键不存在会触发 __newindex， */
        state.Push("fallback");
        state.SetTable(-2, "key1"); /* 此处会调用 __newindex，不会直接写入 T */
        state.Push("fallback");
        state.SetTable(-2, 1);

        /* 使用 GetTable 读取 key1，应返回 __newindex 结果 */
        state.GetTable(-1, "key1");
        state.GetTable(-2, 1);
        LuaStateTestUtils.TestTopElems(state, "metamethod value", "metamethod value");
        state.Pop(2);

        /* 现在使用 RawSetTable 设置同一个字段，绕过元方法 */
        state.Push("rawDirect");
        state.RawSetTable(-2, "key1"); /* 直接设置，不触发 __newindex */
        state.Push(233);
        state.RawSetTable(-2, 1); /* 直接设置，不触发 __newindex */

        /* 直接读取 raw 值：若原始值存在，将返回 "rawDirect" */
        state.GetTable(-1, "key1");
        state.GetTable(-2, 1);
        LuaStateTestUtils.TestTopElems(state, "rawDirect", 233);
    }

    [Fact]
    public void RawLen_MiscCase_String() {
        LuaState state = LuaState.NewState();

        string str;
        void   PushAndTest() {
            state.Push(str);
            Assert.Equal(Encoding.UTF8.GetByteCount(str), state.RawLength(-1));
            state.Pop();
        }

        str = "hello";
        PushAndTest();

        str = "hello, 你好";
        PushAndTest();

        str = "";
        PushAndTest();
    }

    [Fact]
    public void RawLen_MiscCase_Table() {
        LuaState state = LuaState.NewState();

        state.NewTable();

        Assert.Equal(0, state.RawLength(-1));

        state.Push("a");
        state.RawSetTable(-2, 1);
        state.Push("b");
        state.RawSetTable(-2, 2);
        state.Push("c");
        state.RawSetTable(-2, 3);

        /* RawLength 应返回数组部分长度，此处期望 3 */
        Assert.Equal(3, state.RawLength(-1));

        /* 数组截断，应该只剩一个元素 */
        state.PushNil();
        state.RawSetTable(-2, 2);
        Assert.Equal(1, state.RawLength(-1));
    }

    [Fact]
    public void RawEquals_MiscCase() {
        LuaState state = LuaState.NewState();
        state.LuaCheckStack(40);

        /* bool/light user data */
        state.Push(true);
        state.Push(false);
        state.PushNil();
        state.Push(IntPtr.Zero);
        state.Push(IntPtr.Zero);
        state.Push((IntPtr)111);
        Assert.False(state.RawEquals(1, 2));
        Assert.False(state.RawEquals(2, 3));
        Assert.True(state.RawEquals(4, 5));
        Assert.False(state.RawEquals(5, 6));
        Assert.False(state.RawEquals(3, 9999));
        state.ClearFrame();

        /* number */
        state.Push(123);
        state.Push(123);
        state.Push(321);
        state.Push(321.0);
        Assert.True(state.RawEquals(1, 2));
        Assert.False(state.RawEquals(1, 3));
        Assert.True(state.RawEquals(4, 3));
        state.ClearFrame();

        /* string */
        state.Push("foo");
        state.Push("foo");
        state.Push("3.14159");
        Assert.True(state.RawEquals(1, 2));
        Assert.False(state.RawEquals(1, 3));
        state.ClearFrame();

        /* table */
        state.NewTable();  // 创建一个 table 并压入栈顶
        state.Push(state.ToString(-1, out _));
        state.Copy(-2, -1);
        Assert.True(state.RawEquals(-1, -2));
        state.NewTable();
        Assert.False(state.RawEquals(-1, -2));
        state.ClearFrame();

        /* function */
        LuaCFunction dummyFunc = s => 0;
        LuaCFunction dummyFunc2 = s => 0;
        state.Push(dummyFunc);
        state.Push(dummyFunc2);
        state.Push("cclosure upvalue");
        state.Push(dummyFunc, 1); /* c closure */
        state.Push(dummyFunc);
        Assert.True(state.RawEquals(1, 4));
        Assert.False(state.RawEquals(1, 2));
        Assert.False(state.RawEquals(1, 3));
        Assert.False(state.RawEquals(4, 2));
        state.ClearFrame();

        /* none/nil */
        state.Push(123);
        state.PushNil();
        state.PushNil();
        Assert.False(state.RawEquals(1, 1000)); /* none */
        Assert.True(state.RawEquals(2, 3));     /* nil */
        Assert.False(state.RawEquals(2, 9999)); /* nil */
        state.ClearFrame();
    }

    [Fact]
    public void Compare_MiscCase_Number() {
        LuaState state = LuaState.NewState();

        /* 比较相等的数字 */
        state.Push(100);
        state.Push(100.0);
        Assert.True(state.Compare(Comp.EQ, 1, 2));
        Assert.True(state.Compare(Comp.LE, 1, 2));
        Assert.False(state.Compare(Comp.LT, 1, 2));
        state.ClearFrame();

        /* 比较负数，注意顺序：要求 -50 < -25 */
        state.Push(-50.0);
        state.Push(-25);
        /* 将较小的数置于较低位置：比对索引 -2 与 -1 */
        Assert.False(state.Compare(Comp.EQ, 2, 1));
        Assert.True(state.Compare(Comp.LT, 1, 2));
        Assert.True(state.Compare(Comp.LE, 1, 2));
        state.ClearFrame();

        /* 极端数据：比较极大值 */
        state.Push(double.MaxValue);
        state.Push(double.MaxValue);
        Assert.True(state.Compare(Comp.EQ, 1, 2));
        state.ClearFrame();
    }

    [Fact]
    public void Compare_MiscCase_String() {
        LuaState state = LuaState.NewState();

        /* 相等的字符串 */
        state.Push("apple");
        state.Push("apple");
        Assert.True(state.Compare(Comp.EQ, 2, 1));
        Assert.True(state.Compare(Comp.LE, 2, 1)); /* 相等时 <= 返回 true */
        Assert.False(state.Compare(Comp.LT, 2, 1));
        state.ClearFrame();

        /* 不相等的字符串： "apple" 与 "banana" */
        state.Push("apple");
        state.Push("banana");
        /* 对比应为 "apple" < "banana"（注意顺序） */
        Assert.False(state.Compare(Comp.EQ, 2, 1));
        Assert.True(state.Compare(Comp.LT, 1, 2));
        Assert.True(state.Compare(Comp.LE, 1, 2));
        Assert.False(state.Compare(Comp.LT, 2, 1));
        state.ClearFrame();
    }

    [Fact]
    public void Compare_MiscCase_DifferentTypes() {
        LuaState state = LuaState.NewState();

        state.Push(123);
        state.Push(123.0);
        state.Push(122.99999);
        Assert.True(state.Compare(Comp.EQ, 1, 2));
        Assert.False(state.Compare(Comp.LT, 1, 3));
        Assert.False(state.Compare(Comp.LE, 1, 3));
        Assert.True(state.Compare(Comp.LE, 1, 1));
        Assert.False(state.Compare(Comp.LT, 1, 1));
        Assert.True(state.Compare(Comp.LE, 2, 2));
        Assert.True(state.Compare(Comp.LT, 3, 2));
        state.ClearFrame();

        state.Push("00123");
        state.Push("123");
        Assert.False(state.Compare(Comp.EQ, 1, 2));
        Assert.True(state.Compare(Comp.LT, 1, 2)); /* 按 ASCII 字典序比较，“0” 小于 “1”，因此可能返回 LT 或 LE 为 true */
        state.ClearFrame();
    }

    [Fact]
    public void Compare_MiscCese_WithMetatable() {
        LuaState state = LuaState.NewState();

        /* 构造两个表，并设置相同元表，在元表中定义 __lt 元方法 */
        state.NewTable(); /* table1 */
        state.Push(10);
        state.SetTable(-2, "value");

        state.NewTable(); /* table2 */
        state.Push(20);
        state.SetTable(-2, "value");

        /* 新建元表，并设置 __lt 比较函数 */
        state.NewTable(); /* metatable */
        LuaCFunction eqFunc = s => {
            s.GetTable(-2, "value"); /* 获得 table1 的 "value" */
            s.GetTable(-2, "value"); /* 获得 table2 的 "value" */
            s.Push(s.Compare(Comp.EQ, -2, -1));
            return 1;
        };
        LuaCFunction ltFunc = s => {
            s.GetTable(-2, "value"); /* 获得 table1 的 "value" */
            s.GetTable(-2, "value"); /* 获得 table2 的 "value" */
            s.Push(s.Compare(Comp.LT, -2, -1));
            return 1;
        };
        state.Push(ltFunc);
        state.SetTable(-2, "__lt"); /* metatable["__lt"] = ltFunc */
        state.Push(eqFunc);
        state.SetTable(-2, "__eq"); /* metatable["__eq"] = ltFunc */

        /* 将元表分别设置给 table1 和 table2 */
        state.PushValue(-1); /* 复制元表 */
        state.SetMetatable(-3);
        state.SetMetatable(-3);

        Assert.False(state.Compare(Comp.EQ, -2, -1));
        Assert.True(state.Compare(Comp.LT, -2, -1)); /* 测试：table1 < table2 应通过元方法比较返回 true */
        Assert.True(state.Compare(Comp.LE, -2, -1)); /* 测试 LE：table1 <= table2 应返回 true */

        /* 修改 table1 的值，使两表相等，应使 LT 返回 false，但 LE 返回 true */
        state.Push(15);
        state.SetTable(-2, "value");
        state.Push(15);
        state.SetTable(-3, "value");
        Assert.True(state.Compare(Comp.EQ, -2, -1));
        Assert.False(state.Compare(Comp.LT, -2, -1));
        Assert.True(state.Compare(Comp.LE, -2, -1));
        state.ClearFrame();
    }

    [Fact]
    public void Arith_MiscCaase_BasicOperation() {
        LuaState state = LuaState.NewState();

        LuaStateTestUtils.TestIntArith(state, Op.ADD, 10, 13, 23);
        LuaStateTestUtils.TestIntArith(state, Op.ADD, long.MaxValue - 5, 10, checked(long.MinValue + 4));
        LuaStateTestUtils.TestFltArith(state, Op.ADD, 5, 3.2, 8.2);
        LuaStateTestUtils.TestIntArith(state, Op.ADD, 10, 13, 23);
        LuaStateTestUtils.TestIntArith(state, Op.SUB, 100, -300, 400);
        LuaStateTestUtils.TestFltArith(state, Op.SUB, 100, -300, 400);
        LuaStateTestUtils.TestIntArith(state, Op.IDIV, 7, 2, 3);
        LuaStateTestUtils.TestIntArith(state, Op.IDIV, -7, 2, -4);
        LuaStateTestUtils.TestIntArith(state, Op.SHR, 0x80000000, 1, 0x40000000);
        LuaStateTestUtils.TestIntArith(state, Op.BNOT, 0x00000000, 0, -1);
        LuaStateTestUtils.TestFltArith(state, Op.DIV, 45, 2, 22.5);
    }

    [Fact]
    public void Arith_MiscCase_WithMetatable() {
        LuaState state = LuaState.NewState();

        /* metatable["__add"] = addMetamethod */
        LuaCFunction addMetamethod = s => {
            /* 在元方法中，我们假定两个参数都是 table，取其 "value" 字段相加后加 100 */
            s.GetTable(1, "value"); /* 取第一个 table 的 value */
            s.GetTable(2, "value"); /* 取第二个 table 的 value */
            double v1 = s.ToNumber(-2, out bool ok1);
            double v2 = s.ToNumber(-1, out bool ok2);
            s.Pop(2);
            double sum = v1 + v2 + 100;
            s.Push(sum);
            return 1;
        };
        LuaStateMockUtils.CreateTable(state, "__add", addMetamethod);

        /* 给 table1 和 table2 设置相同的元表 */
        LuaStateMockUtils.CreateTables(state, 2, true);
        LuaStateMockUtils.SetTable(state, -1, "value", 10);
        LuaStateMockUtils.SetTable(state, -2, "value", 20);

        /* 执行加法运算，此时两个 table 没有直接的加法操作，应调用元方法 __add */
        /* 预期结果：10 + 20 + 100 = 130 */
        LuaStateTestUtils.TestArith(state, Op.ADD, () => {
            LuaStateTestUtils.TestValue(state, -1, new LuaValue(130));
            return true;
        });
        state.ClearFrame();
    }

    [Fact]
    public void Next_Case_1() {
        LuaState state = LuaState.NewState();

        LuaStateMockUtils.CreateTable(
            state, "c", "whocare", //
            1, 10,                 //
            "b", "whocare",        //
            2, 20,                 //
            3, 30,                 //
            "haha", "whocare",     //
            4, 40,                 //
            "a", "whocare",        //
            5, 50                  //
        );

        state.PushNil();
        for (int i = 1; state.Next(-2); i++) {
            if (i <= 5) {
                LuaStateTestUtils.TestValue(state, -2, new LuaValue(i));
                LuaStateTestUtils.TestValue(state, -1, new LuaValue(i * 10));
            }
            state.Pop();
        }
        state.Push(3);
        for (int i = 4; state.Next(-2); i++) {
            if (i <= 5) {
                LuaStateTestUtils.TestValue(state, -2, new LuaValue(i));
                LuaStateTestUtils.TestValue(state, -1, new LuaValue(i * 10));
            }
            state.Pop();
        }
    }

    [Fact]
    public void LuaConcat_MiscCase() {
        LuaState state = LuaState.NewState();

        void TestConcat(int count, string expected) {
            state.LuaConcat(count);
            string result = state.ToString(-1, out bool success);
            Assert.True(success);
            Assert.Equal(expected, result);
            state.Pop(1);
        }

        TestConcat(0, "");

        state.Push("hello");
        state.Push("world");
        TestConcat(2, "helloworld");

        LuaCFunction concatFunc = s => {
            s.Push("concat");
            s.PushValue(-2);
            s.Concat(2);
            return 1;
        };
        LuaStateMockUtils.CreateTable(
            state, //
            "__concat", concatFunc
        );
        LuaStateMockUtils.CreateTables(state, 2, true);
        state.Push(" hello");
        state.Push("");
        state.Push(" ");
        state.Push("");
        state.Push("world");
        state.Push(" ");
        state.Push(233);
        state.Push("");
        state.Push(0.666);
        state.Push("");
        TestConcat(12, "concatconcat hello world 2330.666");
    }

    [Fact]
    public void LuaGetLength_MiscCase() {
        LuaState state = LuaState.NewState();

        /* 返回字符串的 UTF8 长度 */
        state.Push("hello");
        state.LuaGetLength(-1);  // 将 "hello" 的长度压入栈顶
        LuaStateTestUtils.TestValue(state, -1, new LuaValue(5));
        state.Pop(2); /* 弹出字符串和结果 */

        /* 数组部分 */
        LuaStateMockUtils.CreateTable(
            state,          //
            1, 10,          //
            2, 20,          //
            3, 30,          //
            4, 40,          //
            "a", "whocare", //
            5, 50,          //
            "b", "whocare", //
            6, 60,          //
            8, 80           //
        );
        state.LuaGetLength(-1); /* 读取表长度 */
        LuaStateTestUtils.TestValue(state, -1, new LuaValue(6));
        state.Pop(2);

        /* 带 __len 元方法的表 —— 使用元方法返回固定长度 */
        LuaCFunction lenFunc = s => {
            s.Push(233);
            return 1;
        };
        LuaStateMockUtils.CreateTable(
            state, //
            "__len", lenFunc
        );
        LuaStateMockUtils.CreateTables(state, 1, true);
        state.LuaGetLength(-1); /* 调用 __len 元方法 */
        LuaStateTestUtils.TestValue(state, -1, new LuaValue(233));
        state.Pop(2);

        /* 不支持长度操作的对象 */
        state.Push(123); /* 数值没有长度意义 */
        Assert.Throws<LuaRuntimeError>(() => state.LuaGetLength(-1));
        state.Pop(1);
    }

    [Fact]
    public void StringToNumber_MiscCase() {
        LuaState state = LuaState.NewState();

        void TestStringToNumber(string str, double expected, bool failed = false) {
            int consumed = state.StringToNumber(str);
            if (failed) {
                Assert.True(consumed == 0);
                return;
            }
            Assert.True(consumed == str.Length); /* 因为一定是 ascii 所以就直接用 String.Length 了 */
            double num = state.ToNumber(-1, out bool success);
            Assert.True(success);
            Assert.Equal(expected, num);
            state.Pop(1);
        }

        TestStringToNumber("123", 123);
        TestStringToNumber("45.67", 45.67);
        TestStringToNumber(" -0xFF", -255);
        TestStringToNumber("     078     ", 78);
        TestStringToNumber("     78.3.3     ", 0, true);
        TestStringToNumber("xia", 0, true);
        TestStringToNumber("123e3", 123000);
        TestStringToNumber("123E-3", 0.123);
        TestStringToNumber("1.23e3", 1230);
        TestStringToNumber("0x123.45p2", 1165.078125);
        TestStringToNumber("-0x0.45P-3", -0.03369140625);
        TestStringToNumber("-0x0.45PP", 0, true);
    }

    [Fact]
    public void GetUpvaludId_MiscCase() {
        LuaState state = LuaState.NewState();

        /* 构造闭包，有 1 个上值 */
        LuaCFunction dummyFunc = s => 0;
        state.Push("my upvalue");
        state.Push(dummyFunc, 1);
        int id1 = state.GetUpvalueId(-1, 1);

        state.PushValue(-1);
        int id2 = state.GetUpvalueId(-1, 1);

        state.Push("my upvalue");
        state.Push("my upvalue");
        state.Push(dummyFunc, 2);
        int id3 = state.GetUpvalueId(-1, 2);

        Assert.Equal(id1, id2);
        Assert.NotEqual(id2, id3);
    }

    [Fact]
    public void Call_NormalCase_NoContinuation() {
        LuaState state = LuaState.NewState();

        /* 定义一个求和函数，取两个参数相加，将结果压回栈顶 */
        LuaCFunction sumFunc = s => {
            double a = s.ToNumber(1, out bool ok1);
            double b = s.ToNumber(2, out bool ok2);
            if (!ok1 || !ok2)
                Assert.Fail("Invalid numbers");
            s.Pop(2);
            s.Push(a + b);
            return 1;
        };

        state.Push(sumFunc);
        state.Push(10);
        state.Push(20);
        /* 调用函数，参数个数=2，期望结果个数=1（调用不含 continuation） */
        state.Call(2, 1);
        double result = state.ToNumber(-1, out bool success);
        Assert.True(success);
        Assert.Equal(30, result);
        state.Pop(); /* 清除结果 */
    }
    [Fact]
    public void Call_NormalCase_Continuation() {
        LuaState state  = LuaState.NewState();
        LuaState thread = LuaState.NewThread(state);

        thread.Push(CommonTestUtils.CommonCoroCFunction);
        thread.Push(2);
        thread.Push(3);
        thread.Resume(state, 2);
        LuaStateTestUtils.TestTopElems(thread, 6);

        thread.Push(CommonTestUtils.CommonCoroCFunction);
        thread.NewTable(); /* 不可转为 number */
        thread.Push(3);
        thread.Resume(state, 2);
        thread.Resume(state, 0);
        LuaStateTestUtils.TestTopElems(thread, 1001);
    }

    [Fact]
    public void Call_ErrorCase_1() {
        LuaState state = LuaState.NewState();

        LuaCFunction errorFunc = s => { throw new LuaRuntimeError("Test error"); };

        state.Push(errorFunc);
        try {
            state.Call(0, 0);
            Assert.Fail("Expected exception was not thrown.");
        } catch (Exception ex) {
            Assert.Contains("thread abort", ex.Message);
        }
    }

    [Fact]
    public void SetPanic_Case_1() {
        LuaState state = LuaState.NewState();

        Assert.Equal(503, state.LuaVersion);

        bool         triggeredPanic = false;
        LuaCFunction panic = s => {
            Assert.Equal(state, s);
            triggeredPanic = true;
            return 0;
        };
        LuaCFunction errorFunc = s => { throw new LuaRuntimeError("Test error"); };

        state.Push(errorFunc);
        state.SetPanic(panic);
        try {
            state.Call(0, 0);
        } catch (LuaException) { }
        Assert.Equal(ThreadStatus.ERRRUN, state.ThreadStatus);
        Assert.Equal(ThreadStatus.ERRRUN, state.ThreadStatus);
        Assert.True(triggeredPanic);
        LuaStateTestUtils.TestTopElems(state, "Test error");
    }

    [Fact]
    public void PCall_Case_Continuation() {
        /* 主线程的连续函数通常用于其他线程异常后恢复 */
        LuaState state  = LuaState.NewState();
        LuaState thread = LuaState.NewThread(state);

        LuaCFunction threadFunc3 = (s) => {
            Console.WriteLine("threadFunc3");
            throw new LuaRuntimeError("threadFunc3");
        };
        LuaKFunction threadFunc2Cont = (s, status, ctx) => {
            Console.WriteLine("threadFunc2Cont");
            return 0;
        };
        LuaCFunction threadFunc2 = (s) => {
            Console.WriteLine("threadFunc2");
            s.Push(threadFunc3);
            s.Call(0, 0);
            return 0;
        };
        LuaCFunction threadFunc1 = (s) => {
            Console.WriteLine("threadFunc1");
            s.Push(threadFunc2);
            s.PCall(0, 0, 0, 0, threadFunc2Cont);
            return 0;
        };
        LuaCFunction func3 = (s) => {
            thread.Push(threadFunc1);
            thread.Resume(state, 0);
            return 0;
        };

        state.Push(func3);
        state.PCall(0, 0, 0);
    }

    [Fact]
    public void PCall_Case_ErrorFunc() {
        LuaState state = LuaState.NewState();

        Counter      errorFunc2Counter = new Counter();
        LuaCFunction errorFunc2 = (s) => {
            errorFunc2Counter++;
            Console.WriteLine("errorFunc2");
            return 1;
        };

        LuaCFunction pfunc2 = (s) => {
            state.Push(errorFunc2);
            state.Push(CommonTestUtils.InvalidCFunc);
            state.PCall(0, 0, -2); /* 应该被触发的 errorFunc */
            throw new LuaRuntimeError("hahaha");
        };

        LuaCFunction func1 = (s) => {
            state.Push(pfunc2);
            state.PCall(0, 0, 0);
            return 0;
        };

        state.Push(CommonTestUtils.InvalidErrorFunc); /* 不应该被触发的 errorFunc */
        state.Push(func1);
        state.PCall(0, 0, -2);
        Assert.Equal(1, errorFunc2Counter.Count);
    }

    [Fact]
    public void PCall_Case_RethrowInErrorFunc() {
        LuaState state = LuaState.NewState();

        Counter      errorFunc1Counter = new Counter();
        LuaCFunction errorFunc1 = (s) => {
            errorFunc1Counter++;
            Console.WriteLine($"errorFunc1: {errorFunc1Counter.Count}");
            if (errorFunc1Counter.Count < 10) {
                throw new LuaRuntimeError($"error func rethrow: {errorFunc1Counter.Count}");
            }
            return 1;
        };

        state.Push(errorFunc1);
        state.Push(CommonTestUtils.InvalidCFunc);
        state.PCall(0, 0, -2);
        Assert.Equal(10, errorFunc1Counter.Count);
        LuaStateTestUtils.TestTopElems(state, "error func rethrow: 9");
    }

    [Fact]
    public void PCall_Case_CClosure() {
        LuaState state = LuaState.NewState();

        LuaCFunction func1 = (s) => {
            long   i   = s.ToInteger(LuaConst.UpvalueLdx(1));
            string str = s.ToString(LuaConst.UpvalueLdx(2), out bool success);
            s.Push(i);
            s.Push(str);
            s.Arith(Op.ADD);
            Assert.Equal(579, s.ToInteger(-1));
            return 1;
        };

        state.Push(123);
        state.Push("456");
        state.Push(func1, 2);
        state.PCall(0, 1, 0);
        LuaStateTestUtils.TestTopElems(state, 579);
    }

    [Fact]
    public void PCall_Case_ErrorCClosure() {
        LuaState state = LuaState.NewState();

        LuaCFunction func1 = (s) => {
            long   i   = s.ToInteger(LuaConst.UpvalueLdx(1));
            string str = s.ToString(LuaConst.UpvalueLdx(2), out bool success);
            s.Push(i);
            s.Push(str);
            s.Arith(Op.ADD);
            Assert.Equal(579, s.ToInteger(-1));
            throw new Exception("test normal exception.");
        };

        state.Push(123);
        state.Push("456");
        state.Push(func1, 2);
        Assert.Equal(ThreadStatus.ERRRUN, state.PCall(0, 0, 0));
    }

    [Fact]
    public void PCall_Case_Metamethod() {
        LuaState state = LuaState.NewState();

        LuaCFunction callFunc = (s) => {
            s.Arith(Op.MUL);
            s.Push(4);
            s.Push(5);
            return 3;
        };

        LuaStateMockUtils.CreateTable(state, "__call", callFunc);
        LuaStateMockUtils.CreateTables(state, 1, true);
        state.Push("1");
        state.Push(3);
        state.PCall(2, 5, 0);
        LuaStateTestUtils.TestTopElems(state, 3, 4, 5, CommonTestUtils.NIL, CommonTestUtils.NIL);
    }

    [Fact]
    public void PCall_Case_UnrollError() {
        LuaState state  = LuaState.NewState();
        LuaState thread = LuaState.NewThread(state);

        LuaKFunction kfunc = (s, status, ctx) => {
            Console.WriteLine($"kfunc{ctx}, {status}");
            throw new LuaRuntimeError("hahaha"); /* 两次触发，第二次杀死线程 */
        };

        LuaCFunction func2 = (s) => {
            s.Push(CommonTestUtils.InvalidCFunc);
            s.Call(0, 0);
            return 0;
        };

        LuaCFunction func1 = (s) => {
            s.Push(func2);
            s.PCall(0, 0, 0, 1, kfunc);
            return 0;
        };

        LuaCFunction threadFunc = (s) => {
            s.Push(func1);
            s.PCall(0, 0, 0, 0, kfunc);
            return 0;
        };

        thread.Push(threadFunc);
        thread.Resume(state, 0);
        Assert.Equal(ThreadStatus.ERRRUN, thread.ThreadStatus);
        Assert.Equal(ThreadStatus.ERRRUN, thread.Resume(state, 0));
        LuaStateTestUtils.TestTopElems(thread, "cannot resume dead coroutine");
    }

    [Fact]
    public void PCall_Case_MultiYield_1() {
        LuaState state  = LuaState.NewState();
        LuaState thread = LuaState.NewThread(state);

        Counter      counter    = new Counter();
        LuaKFunction yieldkfunc = (s, status, ctx) => {
            counter++;
            LuaStateTestUtils.TestTopElems(s, 3);
            Console.WriteLine($"kfunc{ctx}, {status}");
            return 3;
        };
        LuaKFunction kfunc = (s, status, ctx) => {
            counter++;
            if (ctx == 1) {
                LuaStateTestUtils.TestTopElems(s, 1, 2, 3);
            }
            Console.WriteLine($"kfunc{ctx}, {status}");
            return 0;
        };

        LuaCFunction yieldFunc = (s) => {
            s.Push(1);
            s.Push(2);
            return s.Yield(2, 2, yieldkfunc);
        };

        LuaCFunction func1 = (s) => {
            s.Push(yieldFunc);
            s.PCall(0, 3, 0, 1, kfunc); /* 这里期待 3 个返回值，会在 kfunc 中获取 */
            return 0;
        };

        LuaCFunction threadFunc = (s) => {
            s.Push(func1);
            s.PCall(0, 0, 0, 0, kfunc);
            return 0;
        };

        thread.Push(threadFunc);
        thread.Resume(state, 0);
        Assert.Equal(ThreadStatus.YIELD, thread.ThreadStatus);
        Assert.Equal(0, counter.Count);
        LuaStateTestUtils.TestTopElems(thread, 1, 2);
        thread.Push(3);
        Assert.Equal(ThreadStatus.OK, thread.Resume(state, 3));
        Assert.Equal(3, counter.Count);
    }

    [Fact]
    public void PCall_Case_MultiYield_2() {
        LuaState state  = LuaState.NewState();
        LuaState thread = LuaState.NewThread(state);

        Counter      counter = new Counter();
        LuaKFunction kfunc = (s, status, ctx) => {
            counter++;
            Console.WriteLine($"kfunc{ctx}, {status}");
            return 0;
        };

        LuaCFunction yieldFunc = (s) => {
            s.Push(1);
            s.Push(2);
            return s.Yield(2); /* no continuation */
        };

        LuaCFunction func1 = (s) => {
            s.Push(yieldFunc);
            s.PCall(0, 0, 0, 1, kfunc);
            return 0;
        };

        LuaCFunction threadFunc = (s) => {
            s.Push(func1);
            s.PCall(0, 0, 0, 0, kfunc);
            return 0;
        };

        thread.Push(threadFunc);
        thread.Resume(state, 0);
        Assert.Equal(ThreadStatus.YIELD, thread.ThreadStatus);
        Assert.Equal(0, counter.Count);
        LuaStateTestUtils.TestTopElems(thread, 1, 2);
        thread.Push(3);
        Assert.Equal(ThreadStatus.OK, thread.Resume(state, 3));
        Assert.Equal(2, counter.Count);
    }
}

}
