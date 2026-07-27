using Moongate.Core.Interfaces;
using Moongate.Scripting;
using MoonSharp.Interpreter;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Scripting;

public class LoopAffineInvokeMarshallerTests
{
    private sealed class StubLoopThread : ILoopThread
    {
        public StubLoopThread(bool onLoop)
        {
            IsOnLoopThread = onLoop;
        }

        public bool IsOnLoopThread { get; }
    }

    [Fact]
    public void OffLoopThread_PostsToDispatcher_AndReturnsNil()
    {
        var dispatcher = new MainThreadDispatcherService();
        var marshaller = new LoopAffineInvokeMarshaller(new StubLoopThread(false), dispatcher);
        var ran = false;

        var result = marshaller.Invoke(() =>
            {
                ran = true;

                return DynValue.NewNumber(42);
            }
        );

        Assert.False(ran);
        Assert.True(result.IsNil());
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.DrainPending();

        Assert.True(ran);
    }

    [Fact]
    public void OffLoopThread_DroppingNonNilResult_Warns()
    {
        var sink = new ListSink();
        var dispatcher = new MainThreadDispatcherService();
        var marshaller = new LoopAffineInvokeMarshaller(
            new StubLoopThread(false),
            dispatcher,
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger()
        );

        marshaller.Invoke(() => DynValue.NewNumber(42));

        dispatcher.DrainPending();

        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
            => Events.Add(logEvent);
    }

    [Fact]
    public void OnLoopThread_RunsInline_AndReturnsValue()
    {
        var dispatcher = new MainThreadDispatcherService();
        var marshaller = new LoopAffineInvokeMarshaller(new StubLoopThread(true), dispatcher);
        var ran = false;

        var result = marshaller.Invoke(() =>
            {
                ran = true;

                return DynValue.NewNumber(42);
            }
        );

        Assert.True(ran);
        Assert.Equal(42, result.Number);
        Assert.Equal(0, dispatcher.PendingCount);
    }
}
