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

public class LuaTableTests
{
    private readonly ITestOutputHelper _output;
    public LuaTableTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Fact]
    public void LuaTable_Case_WeakKey() {
        LuaState state = LuaState.NewState();

        LuaStateMockUtils.CreateTable(state, "__mode", "k");
        LuaStateMockUtils.CreateTables(state, 1, true);

        LuaStateMockUtils.SetTable(state, -1, "a", "aaa");
        state.GetTable(-1, "a");
        LuaStateTestUtils.TestTopElems(state, "aaa");
        state.Pop();

        LuaStateMockUtils.Sweep(state);
        CommonTestUtils.SuperGC(); /* 删除弱引用 */
        LuaTable table       = state.GetStack(state.Top - 1).LObject<LuaTable>();
        int      removeCount = table._Sweep();
        Assert.Equal(1, removeCount);
    }

    [Fact]
    public void LuaTable_MiscCase_ToString() {
        LuaState state = LuaState.NewState();
        state.NewTable();
        LuaTable table = state.GetStack(state.Top - 1).LObject<LuaTable>();
        Assert.EndsWith("{}", table.ToString());

        LuaStateMockUtils.SetTable(state, -1, "a", "aaa");
        Assert.EndsWith("{'a':'aaa'}", table.ToString());
    }

    [Fact]
    public void LuaTable_MiscCase() {
        LuaState state = LuaState.NewState();
        LuaStateMockUtils.CreateTable(state, "__mode", "kv"); /* 双弱的表 */
        LuaStateMockUtils.CreateTables(state, 1, true);
        LuaTable table = state.GetStack(state.Top - 1).LObject<LuaTable>();
        Assert.True(table.WeakKey);
        Assert.True(table.WeakValue);

        /* 测试读 nil 键 */
        state.PushNil();
        state.GetTable(-2);
        Assert.Equal(LuaConst.TNIL, state.GetType(-1));
        state.Pop();

        /* 测试写 nil 值 */
        state.Push("233");
        state.SetTable(-2, 233);
        Assert.Equal("233", table.Get(new LuaValue(233)).Str);
        state.Push(233.0);
        state.PushNil();
        state.SetTable(-3);
        Assert.Equal(LuaConst.TNIL, table.Get(new LuaValue(233)).Type.Raw);
        Assert.Equal(LuaConst.TNIL, table.Get(new LuaValue(233.0)).Type.Raw);

        /* 测试读 nil 值 */
        state.GetTable(-1, 233);
        Assert.Equal(LuaConst.TNIL, state.GetType(-1));
        state.Pop();

        /* 模拟四种情况：键值，失效键-值，键-失效值，失效键-失效值 */
        state.Push(100);
        state.SetTable(-2, 1); /* 整型键值永远不失效 */
        state.NewTable();
        // LuaTable subTable1 = state.GetStack(state.Top - 1).LObject<LuaTable>();
        state.Push(200);
        state.SetTable(-3); /* 对象键会失效 */
        state.Push("300");
        state.SetTable(-2, 2); /* 字符串值会失效 */
        state.NewTable();
        // LuaTable subTable2 = state.GetStack(state.Top - 1).LObject<LuaTable>();
        state.SetTable(-2, "3"); /* 字符串键，对象值都会失效 */
        state.Push(400);
        state.SetTable(-2, "4"); /* 字符串键会失效 */

        Assert.Equal(2, table.GetArrayLength());
        LuaStateTestUtils.TestTableKeyValue(state, -1, 1, 100);
        // LuaStateTestUtils.TestTableKeyValue(state, -1, subTable1, 200);
        LuaStateTestUtils.TestTableKeyValue(state, -1, 2, "300");
        // LuaStateTestUtils.TestTableKeyValue(state, -1, "3", subTable2);
        LuaStateTestUtils.TestTableKeyValue(state, -1, "4", 400);

        // subTable1 = subTable2 = null;   /* 清除本地引用，试了一下如果存在这两个引用会导致无法回收，哪怕设为 null */
        LuaStateMockUtils.Sweep(state); /* 清扫所有残留引用 */
        CommonTestUtils.SuperGC();      /* 清理弱引用 */
        // table._Sweep();                 /* 表清理失效键值，不需要这一步 */

        Console.WriteLine(table);
        Assert.Equal(1, table.GetArrayLength()); /* 只剩一个有效 */
        LuaStateTestUtils.TestTableKeyValue(state, -1, 1, 100);
        LuaStateTestUtils.TestTableKeyValue(state, -1, 2, CommonTestUtils.NIL);
        LuaStateTestUtils.TestTableKeyValue(state, -1, "3", CommonTestUtils.NIL);
        LuaStateTestUtils.TestTableKeyValue(state, -1, "4", CommonTestUtils.NIL);
        foreach (var pair in table) {
            if (pair.Key.IsString)
                Assert.NotEqual("3", pair.Value.Str);
            if (pair.Value.IsInt)
                Assert.NotEqual(200, pair.Value.Int);
        }

        /* 测试重设 __mode */
        LuaStateMockUtils.CreateTable(state, "__mode", ""); /* 普通表 */
        LuaDebug.QuietAction(() => { state.SetMetatable(-2); });
        Assert.False(table.WeakKey);
        Assert.False(table.WeakValue);

        /* 测试写 nil 键 */
        Assert.Throws<LuaRuntimeError>(() => {
            state.PushNil();
            state.Push(666);
            state.SetTable(-3);
        });
    }
}

}
