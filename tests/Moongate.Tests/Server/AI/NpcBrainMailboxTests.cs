using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Data.Internal.AI;
using Moongate.Server.Services.AI;

namespace Moongate.Tests.Server.AI;

public class NpcBrainMailboxTests
{
    [Fact]
    public void Clear_QueuedEvents_RemovesAllWithoutRecordingDelivery()
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(4, metrics);
        mailbox.Enqueue(NpcBrainHookType.Activate, Event(NpcBrainEventType.Activate));
        mailbox.Enqueue(NpcBrainHookType.MobileMoved, Event(NpcBrainEventType.MobileMoved, 0x2));

        mailbox.Clear();

        Assert.Empty(mailbox.Dequeue(4));
        Assert.Equal(0, metrics.Current.EventsDelivered);
    }

    [Fact]
    public void Dequeue_LifecycleCombatSpeechAndRangeEvents_PreservesFifoOrder()
    {
        var mailbox = new NpcBrainMailbox(4, new NpcAiMetrics());
        mailbox.Enqueue(NpcBrainHookType.Activate, Event(NpcBrainEventType.Activate));
        mailbox.Enqueue(NpcBrainHookType.Attacked, Event(NpcBrainEventType.Attacked, 0x2));
        mailbox.Enqueue(NpcBrainHookType.SpeechHeard, Event(NpcBrainEventType.SpeechHeard, 0x3));
        mailbox.Enqueue(NpcBrainHookType.MobileEnteredRange, Event(NpcBrainEventType.MobileEnteredRange, 0x4));

        var delivered = mailbox.Dequeue(4);

        Assert.Equal(
            [
                NpcBrainHookType.Activate,
                NpcBrainHookType.Attacked,
                NpcBrainHookType.SpeechHeard,
                NpcBrainHookType.MobileEnteredRange
            ],
            delivered.Select(entry => entry.Hook)
        );
    }

    [Fact]
    public void Dequeue_LimitBelowCount_DeliversAtMostLimitAndRetainsRemainder()
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(4, metrics);
        mailbox.Enqueue(NpcBrainHookType.Activate, Event(NpcBrainEventType.Activate));
        mailbox.Enqueue(NpcBrainHookType.Attacked, Event(NpcBrainEventType.Attacked, 0x2));
        mailbox.Enqueue(NpcBrainHookType.SpeechHeard, Event(NpcBrainEventType.SpeechHeard, 0x3));

        var first = mailbox.Dequeue(2);
        var second = mailbox.Dequeue(2);

        Assert.Equal(2, first.Count);
        Assert.Single(second);
        Assert.Equal(NpcBrainHookType.SpeechHeard, second[0].Hook);
        Assert.Equal(3, metrics.Current.EventsDelivered);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Enqueue_AlternatingRangeTransitions_RetainsTransitionsAndSuppressesOnlyLatestDuplicate(bool enterFirst)
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(8, metrics);
        var firstHook = enterFirst
                            ? NpcBrainHookType.MobileEnteredRange
                            : NpcBrainHookType.MobileLeftRange;
        var firstType = enterFirst
                            ? NpcBrainEventType.MobileEnteredRange
                            : NpcBrainEventType.MobileLeftRange;
        var secondHook = enterFirst
                             ? NpcBrainHookType.MobileLeftRange
                             : NpcBrainHookType.MobileEnteredRange;
        var secondType = enterFirst
                             ? NpcBrainEventType.MobileLeftRange
                             : NpcBrainEventType.MobileEnteredRange;

        mailbox.Enqueue(firstHook, Event(firstType, 0x2));
        mailbox.Enqueue(secondHook, Event(secondType, 0x2));
        mailbox.Enqueue(firstHook, Event(firstType, 0x2));
        mailbox.Enqueue(firstHook, Event(firstType, 0x2));

        var delivered = mailbox.Dequeue(8);

        Assert.Equal([firstHook, secondHook, firstHook], delivered.Select(entry => entry.Hook));
        Assert.Equal(1, metrics.Current.EventsCoalesced);
        Assert.Equal(3, metrics.Current.EventsDelivered);
    }

    [Theory, InlineData(NpcBrainHookType.MobileEnteredRange, NpcBrainEventType.MobileEnteredRange),
     InlineData(NpcBrainHookType.MobileLeftRange, NpcBrainEventType.MobileLeftRange)]
    public void Enqueue_DuplicateRangeTransition_SuppressesDuplicateAndRecordsCoalesced(
        NpcBrainHookType hook,
        NpcBrainEventType type
    )
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(4, metrics);
        mailbox.Enqueue(hook, Event(type, 0x2));

        mailbox.Enqueue(hook, Event(type, 0x2));

        Assert.Single(mailbox.Dequeue(4));
        Assert.Equal(1, metrics.Current.EventsCoalesced);
        Assert.Equal(1, metrics.Current.EventsDelivered);
    }

    [Fact]
    public void Enqueue_FullMailbox_EvictsMovementBeforeSpeechAndSpeechBeforeLifecycleOrCombat()
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(4, metrics);
        mailbox.Enqueue(NpcBrainHookType.Activate, Event(NpcBrainEventType.Activate));
        mailbox.Enqueue(NpcBrainHookType.Attacked, Event(NpcBrainEventType.Attacked, 0x2));
        mailbox.Enqueue(NpcBrainHookType.SpeechHeard, Event(NpcBrainEventType.SpeechHeard, 0x3));
        mailbox.Enqueue(NpcBrainHookType.MobileMoved, Event(NpcBrainEventType.MobileMoved, 0x4));

        mailbox.Enqueue(NpcBrainHookType.Death, Event(NpcBrainEventType.Death));
        mailbox.Enqueue(NpcBrainHookType.Damage, Event(NpcBrainEventType.Damage, 0x5));

        var delivered = mailbox.Dequeue(4);
        Assert.DoesNotContain(delivered, entry => entry.Hook == NpcBrainHookType.MobileMoved);
        Assert.DoesNotContain(delivered, entry => entry.Hook == NpcBrainHookType.SpeechHeard);
        Assert.Equal(
            [
                NpcBrainHookType.Activate,
                NpcBrainHookType.Attacked,
                NpcBrainHookType.Death,
                NpcBrainHookType.Damage
            ],
            delivered.Select(entry => entry.Hook)
        );
        Assert.Equal(2, metrics.Current.EventsDropped);
        Assert.Equal(4, metrics.Current.EventsDelivered);
    }

    [Fact]
    public void Enqueue_LowerPriorityThanFullMailbox_DropsIncomingEvent()
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(4, metrics);
        mailbox.Enqueue(NpcBrainHookType.Activate, Event(NpcBrainEventType.Activate));
        mailbox.Enqueue(NpcBrainHookType.Deactivate, Event(NpcBrainEventType.Deactivate));
        mailbox.Enqueue(NpcBrainHookType.Death, Event(NpcBrainEventType.Death));
        mailbox.Enqueue(NpcBrainHookType.Attacked, Event(NpcBrainEventType.Attacked, 0x2));

        mailbox.Enqueue(NpcBrainHookType.MobileMoved, Event(NpcBrainEventType.MobileMoved, 0x3));

        Assert.Equal(4, mailbox.Dequeue(8).Count);
        Assert.Equal(1, metrics.Current.EventsDropped);
    }

    [Fact]
    public void Enqueue_SecondMovementForSameMobile_ReplacesPendingEventAndRecordsCoalesced()
    {
        var metrics = new NpcAiMetrics();
        var mailbox = new NpcBrainMailbox(4, metrics);
        mailbox.Enqueue(
            NpcBrainHookType.MobileMoved,
            Event(NpcBrainEventType.MobileMoved, 0x2, new(10, 10, 0), new(11, 10, 0))
        );

        mailbox.Enqueue(
            NpcBrainHookType.MobileMoved,
            Event(NpcBrainEventType.MobileMoved, 0x2, new(11, 10, 0), new(12, 10, 0))
        );

        var delivered = Assert.Single(mailbox.Dequeue(4));
        Assert.Equal(new Point3D(12, 10, 0), delivered.Event.ToPosition);
        Assert.Equal(1, metrics.Current.EventsCoalesced);
        Assert.Equal(1, metrics.Current.EventsDelivered);
    }

    private static NpcBrainEvent Event(
        NpcBrainEventType type,
        uint mobileId = 0,
        Point3D? from = null,
        Point3D? to = null
    )
        => new(type, mobileId == 0 ? null : Snapshot(mobileId), FromPosition: from, ToPosition: to);

    private static BrainMobileSnapshot Snapshot(uint serial)
        => new(
            new(serial),
            $"mobile-{serial}",
            false,
            0,
            new(10, 10, 0),
            10,
            10,
            false,
            Serial.Zero,
            false,
            0
        );
}
