using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Types.Commands;
using Serilog.Events;

namespace Moongate.Tests.Server.Data.Internal.Commands;

public class LuaCommandContextTests
{
    [Test]
    public void Constructor_WhenConsoleContext_ShouldExposeNullSession()
    {
        var context = new CommandSystemContext(
            "help",
            [],
            CommandSourceType.Console,
            -1,
            (_, _) => { }
        );

        var luaContext = new LuaCommandContext(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(luaContext.Source, Is.EqualTo(CommandSourceType.Console));
                Assert.That(luaContext.IsInGame, Is.False);
                Assert.That(luaContext.SessionId, Is.Null);
            }
        );
    }

    [Test]
    public void Constructor_WhenInGameContext_ShouldExposeSessionAndIsInGame()
    {
        var context = new CommandSystemContext(
            "hello world",
            ["hello", "world"],
            CommandSourceType.InGame,
            123,
            (_, _) => { }
        );

        var luaContext = new LuaCommandContext(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(luaContext.CommandText, Is.EqualTo("hello world"));
                Assert.That(luaContext.Arguments, Is.EqualTo(new[] { "hello", "world" }));
                Assert.That(luaContext.Source, Is.EqualTo(CommandSourceType.InGame));
                Assert.That(luaContext.IsInGame, Is.True);
                Assert.That(luaContext.SessionId, Is.EqualTo(123));
            }
        );
    }

    [Test]
    public void Print_ShouldForwardToUnderlyingContext()
    {
        string? message = null;
        LogEventLevel? level = null;
        var context = new CommandSystemContext(
            "hello",
            [],
            CommandSourceType.Console,
            -1,
            (m, l) =>
            {
                message = m;
                level = l;
            }
        );
        var luaContext = new LuaCommandContext(context);

        luaContext.Print("test {0}", "ok");

        Assert.Multiple(
            () =>
            {
                Assert.That(message, Is.EqualTo("test ok"));
                Assert.That(level, Is.EqualTo(LogEventLevel.Information));
            }
        );
    }

    [Test]
    public void PrintError_ShouldForwardToUnderlyingContext()
    {
        string? message = null;
        LogEventLevel? level = null;
        var context = new CommandSystemContext(
            "hello",
            [],
            CommandSourceType.Console,
            -1,
            (m, l) =>
            {
                message = m;
                level = l;
            }
        );
        var luaContext = new LuaCommandContext(context);

        luaContext.PrintError("failure {0}", "now");

        Assert.Multiple(
            () =>
            {
                Assert.That(message, Is.EqualTo("failure now"));
                Assert.That(level, Is.EqualTo(LogEventLevel.Error));
            }
        );
    }
}
