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
