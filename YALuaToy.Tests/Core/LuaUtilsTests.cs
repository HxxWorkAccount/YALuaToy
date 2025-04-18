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

public class LuaUtilsTests
{
    private readonly ITestOutputHelper _output;
    public LuaUtilsTests(ITestOutputHelper output) {
        _output = output;
        CommonTestUtils.InitTest();
    }

    [Theory]
    [InlineData("5.3", 5.3)]
    public void StringToDouble_MiscCase(string text, double expected) {
        double value = LuaUtils.StringToDouble(text, out int consumedCharCount);
        Assert.Equal(consumedCharCount, text.Length);
        Assert.Equal(expected, value);
    }
}

}
