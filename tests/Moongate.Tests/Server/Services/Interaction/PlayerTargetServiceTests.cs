using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Interaction;

public class PlayerTargetServiceTests
{
    private sealed class TimerServiceSpy : ITimerService
    {
        public string? LastName { get; private set; }

        public TimeSpan? LastInterval { get; private set; }

        public TimeSpan? LastDelay { get; private set; }

        public bool LastRepeat { get; private set; }

        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            LastName = name;
            LastInterval = interval;
            LastDelay = delay;
            LastRepeat = repeat;

            return "timer-spy";
        }

        public void UnregisterAllTimers() { }

        public bool UnregisterTimer(string timerId)
            => true;

        public int UnregisterTimersByName(string name)
            => 0;

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    [Test]
    public async Task StartAsync_ShouldRegisterPendingCursorCleanupTimer()
    {
        var service = CreateService(out _, out _, out _, out var timerSpy);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(timerSpy.LastName, Is.EqualTo("pending_cursor_cleanup"));
                Assert.That(timerSpy.LastInterval, Is.EqualTo(TimeSpan.FromMinutes(1)));
                Assert.That(timerSpy.LastDelay, Is.EqualTo(TimeSpan.FromMinutes(1)));
                Assert.That(timerSpy.LastRepeat, Is.True);
            }
        );
    }

    [Test]
    public async Task SendTargetCursorAsync_WhenSessionExists_ShouldEnqueuePacketAndPublishEvent()
    {
        var service = CreateService(out var sessionService, out var outgoingQueue, out var eventBus, out _);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        sessionService.Add(session);

        var callbackCalled = false;
        var cursorId = await service.SendTargetCursorAsync(
            session.SessionId,
            _ => callbackCalled = true,
            TargetCursorSelectionType.SelectObject,
            TargetCursorType.Helpful
        );

        var dequeued = outgoingQueue.TryDequeue(out var outgoingPacket);

        Assert.Multiple(
            () =>
            {
                Assert.That(callbackCalled, Is.False);
                Assert.That(cursorId, Is.Not.EqualTo(Serial.Zero));
                Assert.That(dequeued, Is.True);
                Assert.That(outgoingPacket.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outgoingPacket.Packet, Is.TypeOf<TargetCursorCommandsPacket>());
            }
        );

        var targetPacket = (TargetCursorCommandsPacket)outgoingPacket.Packet;
        Assert.Multiple(
            () =>
            {
                Assert.That(targetPacket.CursorTarget, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(targetPacket.CursorType, Is.EqualTo(TargetCursorType.Helpful));
                Assert.That(targetPacket.CursorId, Is.EqualTo(cursorId));
            }
        );

        Assert.That(eventBus.Events.Count, Is.EqualTo(1));
        Assert.That(eventBus.Events[0], Is.TypeOf<TargetRequestCursorEvent>());

        var publishedEvent = (TargetRequestCursorEvent)eventBus.Events[0];
        Assert.Multiple(
            () =>
            {
                Assert.That(publishedEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(publishedEvent.SelectionType, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(publishedEvent.CursorType, Is.EqualTo(TargetCursorType.Helpful));
                Assert.That(publishedEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
                Assert.That(publishedEvent.Callback, Is.Not.Null);
            }
        );
    }

    [Test]
    public async Task SendTargetCursorAsync_WhenSessionDoesNotExist_ShouldReturnZeroAndDoNothing()
    {
        var service = CreateService(out _, out var outgoingQueue, out var eventBus, out _);

        var cursorId = await service.SendTargetCursorAsync(9999, _ => { });

        Assert.Multiple(
            () =>
            {
                Assert.That(cursorId, Is.EqualTo(Serial.Zero));
                Assert.That(outgoingQueue.CurrentQueueDepth, Is.EqualTo(0));
                Assert.That(eventBus.Events, Is.Empty);
            }
        );
    }

    [Test]
    public async Task SendCancelTargetCursorAsync_WhenCursorIsPending_ShouldEnqueueCancelPacket()
    {
        var service = CreateService(out var sessionService, out var outgoingQueue, out _, out _);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        sessionService.Add(session);

        var cursorId = await service.SendTargetCursorAsync(session.SessionId, _ => { });
        await service.SendCancelTargetCursorAsync(session.SessionId, cursorId);

        var firstDequeued = outgoingQueue.TryDequeue(out _);
        var secondDequeued = outgoingQueue.TryDequeue(out var cancelOutgoing);

        Assert.Multiple(
            () =>
            {
                Assert.That(firstDequeued, Is.True);
                Assert.That(secondDequeued, Is.True);
                Assert.That(cancelOutgoing.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(cancelOutgoing.Packet, Is.TypeOf<TargetCursorCommandsPacket>());
            }
        );

        var cancelPacket = (TargetCursorCommandsPacket)cancelOutgoing.Packet;
        Assert.Multiple(
            () =>
            {
                Assert.That(cancelPacket.CursorId, Is.EqualTo(cursorId));
                Assert.That(cancelPacket.CursorType, Is.EqualTo(TargetCursorType.CancelCurrentTargeting));
                Assert.That(cancelPacket.CursorTarget, Is.EqualTo(TargetCursorSelectionType.SelectLocation));
            }
        );

        await service.SendCancelTargetCursorAsync(session.SessionId, cursorId);
        Assert.That(outgoingQueue.CurrentQueueDepth, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_ShouldSendTargetCursorForEventSession()
    {
        var service = CreateService(out var sessionService, out var outgoingQueue, out var eventBus, out _);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        sessionService.Add(session);

        var sourceEvent = new TargetRequestCursorEvent(
            session.SessionId,
            TargetCursorSelectionType.SelectObject,
            TargetCursorType.Harmful,
            _ => { }
        );

        await service.HandleAsync(sourceEvent);

        Assert.That(outgoingQueue.CurrentQueueDepth, Is.EqualTo(1));
        Assert.That(eventBus.Events.Count, Is.EqualTo(0));

        outgoingQueue.TryDequeue(out var outgoing);
        var packet = outgoing.Packet as TargetCursorCommandsPacket;
        Assert.That(packet, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(packet!.CursorTarget, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(packet.CursorType, Is.EqualTo(TargetCursorType.Harmful));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldReturnTrue_ForTargetCursorPacket()
    {
        var service = CreateService(out _, out _, out _, out _);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var result = await service.HandlePacketAsync(session, new TargetCursorCommandsPacket());

        Assert.That(result, Is.True);
    }

    private static PlayerTargetService CreateService(
        out FakeGameNetworkSessionService sessionService,
        out BasePacketListenerTestOutgoingPacketQueue outgoingQueue,
        out NetworkServiceTestGameEventBusService eventBus,
        out TimerServiceSpy timerSpy
    )
    {
        sessionService = new();
        outgoingQueue = new();
        eventBus = new();
        timerSpy = new();

        return new(sessionService, outgoingQueue, eventBus, timerSpy);
    }
}
