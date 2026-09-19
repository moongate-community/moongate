using Moongate.Scripting.Internal;

namespace Moongate.Tests.Scripting.Internal;

public sealed class ScriptErrorParserTests
{
    [Fact]
    public void Parse_ExtractsFileLineAndMessageFromTheLuaPrefix()
    {
        var info = ScriptErrorParser.Parse("Lua-CSharp: [string \"ai/guard.lua\"]:12: attempt to index a nil value (local 'y')", "stack traceback:\n\t...");

        Assert.Equal("ai/guard.lua", info.File);
        Assert.Equal(12, info.Line);
        Assert.Equal("attempt to index a nil value (local 'y')", info.Message);
        Assert.StartsWith("stack traceback:", info.Traceback, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithoutAPrefix_KeepsTheWholeMessageAndLineZero()
    {
        var info = ScriptErrorParser.Parse("budget exceeded", null);

        Assert.Equal("", info.File);
        Assert.Equal(0, info.Line);
        Assert.Equal("budget exceeded", info.Message);
        Assert.Null(info.Traceback);
    }

    [Fact]
    public void FromException_UsesTheFallbackFileWhenTheMessageHasNone()
    {
        var info = ScriptErrorParser.FromException(new InvalidOperationException("engine went asynchronous"), "init.lua");

        Assert.Equal("init.lua", info.File);
        Assert.Equal("engine went asynchronous", info.Message);
    }
}
