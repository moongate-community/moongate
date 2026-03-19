using System.Diagnostics;
using Moongate.Server.Data.Events.Connections;
using Moongate.Server.Data.Events.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Support;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.Server.Services.Events;

public class GameEventBusServiceTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
            => Events.Add(logEvent);
    }

    private sealed class DelayedConnectedListener : IGameEventListener<PlayerConnectedEvent>
    {
        private readonly int _delayMilliseconds;

        public DelayedConnectedListener(int delayMilliseconds)
        {
            _delayMilliseconds = delayMilliseconds;
        }

        public async Task HandleAsync(
            PlayerConnectedEvent gameEvent,
            CancellationToken cancellationToken = default
        )
            => await Task.Delay(_delayMilliseconds, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_ShouldDispatchCommandEnteredEventToTypedListener()
    {
        var bus = new GameEventBusService();
        var listener = new GameEventBusTrackingCommandEnteredListener();
        var commandEvent = new CommandEnteredEvent("help");

        bus.RegisterListener(listener);
        await bus.PublishAsync(commandEvent);

        Assert.That(listener.Received.Count, Is.EqualTo(1));
        Assert.That(listener.Received[0].CommandText, Is.EqualTo("help"));
    }

    [Test]
    public async Task PublishAsync_ShouldDispatchListenersConcurrently()
    {
        var bus = new GameEventBusService();
        var delayMilliseconds = 150;

        bus.RegisterListener(new DelayedConnectedListener(delayMilliseconds));
        bus.RegisterListener(new DelayedConnectedListener(delayMilliseconds));

        var stopwatch = Stopwatch.StartNew();
        await bus.PublishAsync(new PlayerConnectedEvent(7, null, 1));
        stopwatch.Stop();

        Assert.That(
            stopwatch.ElapsedMilliseconds,
            Is.LessThan(delayMilliseconds * 2 - 20),
            $"Listeners were executed sequentially. Elapsed={stopwatch.ElapsedMilliseconds}ms"
        );
    }

    [Test]
    public async Task PublishAsync_ShouldNotifyRegisteredListeners()
    {
        var bus = new GameEventBusService();
        var listener = new GameEventBusTrackingConnectedListener();
        var gameEvent = new PlayerConnectedEvent(42, "127.0.0.1:2593", 123);

        bus.RegisterListener(listener);
        await bus.PublishAsync(gameEvent);

        Assert.That(listener.Received.Count, Is.EqualTo(1));
        Assert.That(listener.Received[0].SessionId, Is.EqualTo(42));
    }

    [Test]
    public async Task PublishAsync_WhenGlobalListenerRegistered_ShouldReceiveAllEvents()
    {
        var bus = new GameEventBusService();
        var allEventsListener = new GameEventBusTrackingAllEventsListener();

        bus.RegisterListener(allEventsListener);

        var connected = new PlayerConnectedEvent(10, "127.0.0.1:2593", 100);
        var disconnected = new PlayerDisconnectedEvent(10, "127.0.0.1:2593", 101);

        await bus.PublishAsync(connected);
        await bus.PublishAsync(disconnected);

        Assert.That(allEventsListener.Received.Count, Is.EqualTo(2));
        Assert.That(allEventsListener.Received[0], Is.EqualTo(connected));
        Assert.That(allEventsListener.Received[1], Is.EqualTo(disconnected));
    }

    [Test]
    public async Task PublishAsync_WhenListenerIsSlow_ShouldLogWarningWithListenerType()
    {
        var previousLogger = Log.Logger;
        var sink = new CapturingSink();
        Log.Logger = new LoggerConfiguration()
                     .MinimumLevel
                     .Verbose()
                     .WriteTo
                     .Sink(sink)
                     .CreateLogger();

        try
        {
            var bus = new GameEventBusService();
            bus.RegisterListener(new DelayedConnectedListener(150));

            await bus.PublishAsync(new PlayerConnectedEvent(7, null, 1));

            Assert.That(
                sink.Events.Any(
                    logEvent =>
                        logEvent.Level == LogEventLevel.Warning &&
                        logEvent.MessageTemplate.Text.Contains("Slow game event listener") &&
                        logEvent.Properties.TryGetValue("ListenerType", out var listenerType) &&
                        listenerType.ToString().Contains(nameof(DelayedConnectedListener))
                ),
                Is.True
            );
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Test]
    public async Task PublishAsync_WhenOneListenerFails_ShouldContinueOtherListeners()
    {
        var bus = new GameEventBusService();
        var failing = new GameEventBusFailingConnectedListener();
        var tracking = new GameEventBusTrackingConnectedListener();

        bus.RegisterListener(failing);
        bus.RegisterListener(tracking);

        await bus.PublishAsync(new PlayerConnectedEvent(7, null, 1));

        Assert.That(tracking.Received.Count, Is.EqualTo(1));
        Assert.That(tracking.Received[0].SessionId, Is.EqualTo(7));
    }
}
