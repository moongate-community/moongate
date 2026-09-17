using Moongate.Server.Services.Console;
using Moongate.Server.Services.Console.Internal.Logging;
using Moongate.Tests.TestSupport.Console;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace Moongate.Tests.Server.Services.Console;

public sealed class PromptAwareConsoleSinkTests
{
    [Fact]
    public void Emit_DeliversTheEventInsideTheHiddenPromptWindow()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 20, WindowHeight = 10, BufferHeight = 10 };
        var prompt = new ConsolePromptService(driver, interactive: true);
        prompt.ShowPrompt();
        var inner = new RecordingLogEventSink(driver);
        var sink = new PromptAwareConsoleSink(prompt, new LoggerConfiguration().WriteTo.Sink(inner).CreateLogger());
        var marker = driver.Operations.Count;

        sink.Emit(CreateEvent("hello"));

        Assert.Single(inner.Events);
        var after = driver.Operations.Skip(marker).ToArray();
        Assert.Equal("pos:0,9", after[0]);
        Assert.Contains("writeline:emitted", after);
        Assert.Contains("write:MG [LOCKED]> ", after);
        Assert.True(Array.IndexOf(after, "writeline:emitted") < Array.LastIndexOf(after, "write:MG [LOCKED]> "));
    }

    [Fact]
    public void Emit_NonInteractiveStillReachesTheInnerLogger()
    {
        var driver = new RecordingConsoleDriver();
        var prompt = new ConsolePromptService(driver, interactive: false);
        var inner = new RecordingLogEventSink(driver);
        var sink = new PromptAwareConsoleSink(prompt, new LoggerConfiguration().WriteTo.Sink(inner).CreateLogger());

        sink.Emit(CreateEvent("hello"));

        Assert.Single(inner.Events);
    }

    private static LogEvent CreateEvent(string message)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate(message, [new TextToken(message)]),
            []
        );
    }
}
