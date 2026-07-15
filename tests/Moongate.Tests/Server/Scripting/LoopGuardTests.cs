using Moongate.Server.Scripting;
using Moongate.Tests.Support;
using Serilog;
using Serilog.Events;

namespace Moongate.Tests.Server.Scripting;

public class LoopGuardTests
{
    [Fact]
    public void Warn_OffLoop_EmitsWarning()
    {
        var sink = new ListSink();
        var original = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            LoopGuard.Warn(new StubLoopThread(false), "mobile.create");
        }
        finally
        {
            Log.Logger = original;
        }

        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public void Warn_OnLoop_DoesNotWarn()
    {
        var sink = new ListSink();
        var original = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            LoopGuard.Warn(new StubLoopThread(true), "mobile.create");
        }
        finally
        {
            Log.Logger = original;
        }

        Assert.DoesNotContain(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    private sealed class ListSink : Serilog.Core.ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
            => Events.Add(logEvent);
    }
}
