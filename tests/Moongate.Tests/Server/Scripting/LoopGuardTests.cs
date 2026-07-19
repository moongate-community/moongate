using Moongate.Server.Scripting;
using Moongate.Tests.Support;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.Server.Scripting;

public class LoopGuardTests
{
    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
            => Events.Add(logEvent);
    }

    [Fact]
    public void Warn_OffLoop_EmitsWarning()
    {
        var sink = new ListSink();

        LoopGuard.Warn(new StubLoopThread(false), "mobile.create", LoggerFor(sink));

        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public void Warn_OnLoop_DoesNotWarn()
    {
        var sink = new ListSink();

        LoopGuard.Warn(new StubLoopThread(), "mobile.create", LoggerFor(sink));

        Assert.DoesNotContain(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    // A private logger writing to a per-test sink, so nothing touches the global Log.Logger and
    // parallel tests can never write into this sink (which would race the assertions below).
    private static ILogger LoggerFor(ListSink sink)
        => new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
}
