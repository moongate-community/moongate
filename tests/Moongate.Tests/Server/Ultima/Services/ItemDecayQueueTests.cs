using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemDecayQueueTests
{
    private readonly SettableClock _clock = new();
    private readonly ItemDecayQueue _queue;

    public ItemDecayQueueTests()
    {
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "gold" },
                new ItemTemplate { Id = "bottle", DecayMinutes = 5 },
                new ItemTemplate { Id = "statue", Decays = false },
                new ItemTemplate { Id = "gm_marker", Visibility = AccountType.GameMaster },
                new ItemTemplate { Id = "anvil", Movable = false, Decays = true }
            )
        );
        _queue = new(templates, new FakeTileDataService(), _clock);
    }

    [Fact]
    public void Restart_SetsTheDecayTimeAnHourAheadAndTakesItOnlyThen()
    {
        var gold = Ground(0x40000001, "gold");

        _queue.Restart(gold);

        Assert.Equal(_clock.Now.UtcDateTime.AddHours(1), gold.DecayAt);
        _clock.Advance(TimeSpan.FromMinutes(59));
        Assert.Empty(_queue.TakeDue());
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal([gold], _queue.TakeDue());
        Assert.Empty(_queue.TakeDue());
    }

    [Fact]
    public void Restart_UsesTheTemplateDecayMinutes()
    {
        var bottle = Ground(0x40000002, "bottle");

        _queue.Restart(bottle);

        Assert.Equal(_clock.Now.UtcDateTime.AddMinutes(5), bottle.DecayAt);
    }

    [Theory, InlineData("statue"), InlineData("gm_marker"), InlineData("anvil"), InlineData("unknown")]
    public void Restart_AnItemThatDoesNotDecay_HasNoDecayTime(string template)
    {
        var item = Ground(0x40000003, template);

        _queue.Restart(item);
        _clock.Advance(TimeSpan.FromDays(1));

        Assert.Null(item.DecayAt);
        Assert.Empty(_queue.TakeDue());
    }

    [Fact]
    public void Restart_AnItemMadeImmovable_HasNoDecayTime()
    {
        var gold = Ground(0x40000004, "gold");
        gold.Movable = false;

        _queue.Restart(gold);

        Assert.Null(gold.DecayAt);
    }

    [Fact]
    public void Stop_ClearsTheDecayTimeAndTheQueuedEntryNoLongerCounts()
    {
        var gold = Ground(0x40000005, "gold");
        _queue.Restart(gold);

        _queue.Stop(gold);
        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Null(gold.DecayAt);
        Assert.Empty(_queue.TakeDue());
    }

    [Fact]
    public void Restart_Again_CountsOnlyTheLatestTime()
    {
        var gold = Ground(0x40000006, "gold");
        _queue.Restart(gold);
        _clock.Advance(TimeSpan.FromMinutes(30));

        _queue.Restart(gold);
        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Empty(_queue.TakeDue());
        _clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal([gold], _queue.TakeDue());
    }

    [Fact]
    public void Track_KeepsASavedDecayTime_OrStartsOne()
    {
        var saved = Ground(0x40000007, "gold");
        saved.DecayAt = _clock.Now.UtcDateTime.AddMinutes(-1);
        var unsaved = Ground(0x40000008, "gold");

        _queue.Track(saved);
        _queue.Track(unsaved);

        Assert.Equal([saved], _queue.TakeDue());
        Assert.Equal(_clock.Now.UtcDateTime.AddHours(1), unsaved.DecayAt);
    }

    private static ItemEntity Ground(uint serial, string template)
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = template, Amount = 1 };
        item.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));

        return item;
    }
}
