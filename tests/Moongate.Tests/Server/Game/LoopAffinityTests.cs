using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Services.Game;
using Moongate.Tests.Support;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.Server.Game;

public class LoopAffinityTests
{
    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
            => Events.Add(logEvent);
    }

    [Fact]
    public void AssertOnLoop_OffLoopNonStrict_WarnsDoesNotThrow()
    {
        var sink = new ListSink();
        var affinity = new LoopAffinity(
            new StubLoopThread(onLoop: false),
            new MoongateConfig { StrictLoopAffinity = false },
            LoggerFor(sink)
        );

        affinity.AssertOnLoop("item.create");

        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public void AssertOnLoop_OffLoopStrict_Throws()
    {
        var affinity = new LoopAffinity(
            new StubLoopThread(onLoop: false),
            new MoongateConfig { StrictLoopAffinity = true }
        );

        Assert.Throws<InvalidOperationException>(() => affinity.AssertOnLoop("item.create"));
    }

    [Fact]
    public void AssertOnLoop_OnLoop_DoesNothing()
    {
        var sink = new ListSink();
        var affinity = new LoopAffinity(
            new StubLoopThread(onLoop: true),
            new MoongateConfig { StrictLoopAffinity = true },
            LoggerFor(sink)
        );

        affinity.AssertOnLoop("item.create");

        Assert.Empty(sink.Events);
    }

    private static ILogger LoggerFor(ListSink sink)
        => new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
}
