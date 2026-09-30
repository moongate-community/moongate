using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemDecayServiceTests
{
    private readonly SettableClock _clock = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly ItemService _items;
    private readonly ItemDecayService _decay;
    private readonly ItemEntity _bag = Item(0x40000001, "bag");
    private readonly ItemEntity _coins = Item(0x40000002, "gold");
    private readonly ItemEntity _dagger = Item(0x40000003, "dagger");

    public ItemDecayServiceTests()
    {
        var queue = new ItemDecayQueue(
            new ItemTemplateService(
                new StubDataLoaderService().With(
                    new ItemTemplate { Id = "bag" },
                    new ItemTemplate { Id = "gold" },
                    new ItemTemplate { Id = "dagger", DecayMinutes = 90 }
                )
            ),
            new FakeTileDataService(),
            _clock
        );
        _items = TestItems.Create(decay: queue);
        _decay = new(_timers, queue, _items, _view);
        _bag.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _coins.PutInContainer(_bag.Id, new Point2D(40, 40));
        _dagger.PlaceOnGround(MapType.Trammel, new Point3D(1601, 1600, 0));
        _items.Add([_bag, _coins, _dagger]);
    }

    [Fact]
    public async Task StartAsync_RegistersOneRepeatingCheckEveryFiveSeconds()
    {
        await _decay.StartAsync();

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(("item_decay", TimeSpan.FromSeconds(5), true), (timer.Name, timer.Interval, timer.Repeat));
    }

    [Fact]
    public async Task ACheck_DeletesTheDueGroundItemsWithTheirContents_AndTakesThemOffTheScreens()
    {
        await _decay.StartAsync();
        _clock.Advance(TimeSpan.FromHours(1));

        _timers.Fire(_timers.Timers[0].Id);

        Assert.False(_items.TryGet(_bag.Id, out _));
        Assert.False(_items.TryGet(_coins.Id, out _));
        Assert.True(_items.TryGet(_dagger.Id, out _));
        Assert.Equal([$"Disappeared {_bag.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task ACheck_BeforeTheTime_DeletesNothing()
    {
        await _decay.StartAsync();
        _clock.Advance(TimeSpan.FromMinutes(59));

        _timers.Fire(_timers.Timers[0].Id);

        Assert.True(_items.TryGet(_bag.Id, out _));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task ACheck_SkipsAnItemPickedUpInTheMeantime()
    {
        await _decay.StartAsync();
        _items.Hide(_bag);
        _clock.Advance(TimeSpan.FromHours(2));

        _timers.Fire(_timers.Timers[0].Id);

        Assert.True(_items.TryGet(_bag.Id, out _));
        Assert.False(_items.TryGet(_dagger.Id, out _));
    }

    [Fact]
    public async Task StopAsync_UnregistersTheCheck()
    {
        await _decay.StartAsync();

        await _decay.StopAsync();

        Assert.Empty(_timers.Timers);
    }

    private static ItemEntity Item(uint serial, string template)
    {
        return new() { Id = new Serial(serial), TemplateId = template, ItemId = 0x0E76, Amount = 1 };
    }
}
