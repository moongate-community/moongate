using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Timing;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemTimerQueueTests
{
    private readonly SettableClock _clock = new();
    private readonly ItemTimerQueue _queue;

    public ItemTimerQueueTests()
    {
        _queue = new(_clock);
    }

    [Fact]
    public void TakeDue_GivesTheEntriesWhoseTimeHasCome_TheEarliestFirst_Once()
    {
        var now = _clock.Now.ToUnixTimeMilliseconds();
        _queue.Schedule(new Serial(0x40000002), "late", now + 3000);
        _queue.Schedule(new Serial(0x40000001), "early", now + 1000);
        _queue.Schedule(new Serial(0x40000003), "later", now + 9000);

        Assert.Empty(_queue.TakeDue());
        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(["early", "late"], _queue.TakeDue().Select(entry => entry.Name));
        Assert.Empty(_queue.TakeDue());
    }

    [Fact]
    public void ATimerScheduledAgain_IsTakenOnceAtItsLastTime()
    {
        var now = _clock.Now.ToUnixTimeMilliseconds();
        var door = new Serial(0x40000001);
        _queue.Schedule(door, "close", now + 1000);
        _queue.Schedule(door, "close", now + 5000);
        _queue.Schedule(door, "close", now + 5000);

        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(_queue.TakeDue());
        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(now + 5000, Assert.Single(_queue.TakeDue()).DueAt);
    }

    [Fact]
    public void ATimerScheduledAgainManyTimes_DoesNotGrowTheQueue()
    {
        var now = _clock.Now.ToUnixTimeMilliseconds();

        for (var index = 0; index < 10_000; index++)
        {
            _queue.Schedule(new Serial(0x40000001), "idle", now + 3_600_000 + index);
        }

        Assert.InRange(_queue.Count, 1, ItemTimerQueue.CompactAbove);
    }

    [Fact]
    public void Track_QueuesTheTimersTheItemsPropsKeep_AndNothingElse()
    {
        var now = _clock.Now.ToUnixTimeMilliseconds();
        var door = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "door" };
        door.SetProp("timer.close", now - 5);
        door.SetProp("timer.", now - 5);
        door.SetProp("timer.text", "soon");
        door.SetProp("door.open", true);
        _queue.Track(door);
        _queue.Track(new ItemEntity { Id = new Serial(0x40000002), TemplateId = "door" });

        var entry = Assert.Single(_queue.TakeDue());

        Assert.Equal((door.Id, "close", now - 5), (entry.Item, entry.Name, entry.DueAt));
    }
}
