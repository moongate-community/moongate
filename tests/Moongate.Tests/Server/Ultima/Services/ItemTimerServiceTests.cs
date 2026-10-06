using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemTimerServiceTests
{
    private readonly SettableClock _clock = new();
    private readonly RecordingTimerService _wheel = new();
    private readonly RecordingItemScriptService _scripts = new();
    private readonly ItemTimerQueue _queue;
    private readonly ItemService _items;
    private readonly ItemTimerService _timers;

    private readonly ItemEntity _door = new()
        { Id = new Serial(0x40000001), TemplateId = "door", ItemId = 0x0675, Amount = 1 };

    public ItemTimerServiceTests()
    {
        _queue = new(_clock);
        _items = TestItems.Create(timers: _queue);
        _timers = new(_wheel, _queue, _items, _scripts, _clock);
        _scripts.Scripted.Add("door");
        _door.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([_door]);
    }

    [Fact]
    public async Task Check_ReservedInventoryKeepsDueTimerWithoutRunningScript()
    {
        var reservations = new Moongate.Server.Ultima.Services.Items.InventoryReservationService(new StubGameLoop());
        var guard = new Moongate.Server.Ultima.Services.Items.InventoryMutationGuard(
            new Lazy<Moongate.Server.Ultima.Interfaces.IItemService>(() => _items),
            reservations
        );
        var service = new ItemTimerService(_wheel, _queue, _items, _scripts, _clock, guard);
        _items.Equip(_door, new(2), LayerType.Backpack);
        await service.StartAsync();
        service.Start(_door, "close", TimeSpan.FromSeconds(1));
        reservations.TryReserve(new(2), Task.CompletedTask);
        _clock.Advance(TimeSpan.FromSeconds(2));
        Check();
        Assert.True(_door.TryGetProp<long>("timer.close", out _));
        Assert.Empty(_scripts.Calls);
        Assert.False(service.Start(_door, "new", TimeSpan.FromSeconds(1)));
        Assert.False(service.Stop(_door, "close"));
        reservations.Release(new(2));
        Check();
        Assert.Single(_scripts.Calls);
        Assert.False(_door.TryGetProp<long>("timer.close", out _));
    }

    [Fact]
    public async Task StartAsync_RegistersOneRepeatingCheckEverySecond_AndStopAsyncRemovesIt()
    {
        await _timers.StartAsync();

        var timer = Assert.Single(_wheel.Timers);
        Assert.Equal(("item_timers", TimeSpan.FromSeconds(1), true), (timer.Name, timer.Interval, timer.Repeat));

        await _timers.StopAsync();
        Assert.Equal([timer.Id], _wheel.Unregistered);
    }

    [Fact]
    public async Task ATimer_RunsOnTimerOfTheItemsScriptOnce_WhenItsTimeHasCome()
    {
        await _timers.StartAsync();

        Assert.True(_timers.Start(_door, "close", TimeSpan.FromSeconds(20)));
        Assert.Equal(TimeSpan.FromSeconds(20), _timers.Remaining(_door, "close"));
        _clock.Advance(TimeSpan.FromSeconds(19));
        Check();
        Assert.Empty(_scripts.Calls);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Check();
        Check();

        Assert.Equal(["0x40000001 on_timer close"], _scripts.Calls);
        Assert.Null(_timers.Remaining(_door, "close"));
        Assert.False(_door.TryGetProp<long>("timer.close", out _));
    }

    [Fact]
    public async Task ATimerIsAPropOfItsItem_SoAnotherServerRunsItAfterARestart()
    {
        _timers.Start(_door, "close", TimeSpan.FromSeconds(20));
        var saved = _door.Snapshot();

        // The restart: a new queue and item service are given the saved item.
        var queue = new ItemTimerQueue(_clock);
        var items = TestItems.Create(timers: queue);
        var timers = new ItemTimerService(_wheel, queue, items, _scripts, _clock);
        items.Add([saved]);
        await timers.StartAsync();
        _clock.Advance(TimeSpan.FromHours(3));
        Check();

        Assert.Equal(["0x40000001 on_timer close"], _scripts.Calls);
    }

    [Fact]
    public async Task AScriptThatStartsTheTimerAgainInOnTimer_KeepsItRunning()
    {
        await _timers.StartAsync();
        _timers.Start(_door, "pulse", TimeSpan.FromSeconds(5));
        _scripts.OnRun = _ => _timers.Start(_door, "pulse", TimeSpan.FromSeconds(5));

        _clock.Advance(TimeSpan.FromSeconds(5));
        Check();
        Assert.Equal(TimeSpan.FromSeconds(5), _timers.Remaining(_door, "pulse"));
        _clock.Advance(TimeSpan.FromSeconds(5));
        Check();

        Assert.Equal(2, _scripts.Calls.Count);
    }

    [Fact]
    public async Task AStoppedTimer_ATimerStartedAgain_AndAnItemThatIsGone_DoNotRunAtTheOldTime()
    {
        await _timers.StartAsync();
        var chest = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "door", ItemId = 0x0E42, Amount = 1 };
        chest.PlaceOnGround(MapType.Trammel, new Point3D(1601, 1600, 0));
        _items.Add([chest]);
        _timers.Start(_door, "close", TimeSpan.FromSeconds(10));
        _timers.Start(_door, "lock", TimeSpan.FromSeconds(10));
        _timers.Start(chest, "refill", TimeSpan.FromSeconds(10));

        Assert.True(_timers.Stop(_door, "close"));
        Assert.False(_timers.Stop(_door, "close"));
        _timers.Start(_door, "lock", TimeSpan.FromSeconds(60));
        _items.Remove([chest.Id]);
        _clock.Advance(TimeSpan.FromSeconds(10));
        Check();

        Assert.Empty(_scripts.Calls);
        Assert.Equal(TimeSpan.FromSeconds(50), _timers.Remaining(_door, "lock"));
    }

    [Fact]
    public async Task AFailingScript_DoesNotStopTheOtherTimers()
    {
        await _timers.StartAsync();
        _timers.Start(_door, "first", TimeSpan.FromSeconds(1));
        _timers.Start(_door, "second", TimeSpan.FromSeconds(2));
        _scripts.OnRun = _ =>
        {
            if (_scripts.Calls.Count == 1)
            {
                throw new InvalidOperationException("boom");
            }
        };
        _clock.Advance(TimeSpan.FromSeconds(2));

        Check();

        Assert.Equal(2, _scripts.Calls.Count);
    }

    [Theory, InlineData("", 5), InlineData(" ", 5), InlineData("close", 0), InlineData("close", -1),
     InlineData("close", 40000000)]
    public void Start_ABlankNameOrADelayOutOfRange_IsRefused(string name, int seconds)
    {
        Assert.False(_timers.Start(_door, name, TimeSpan.FromSeconds(seconds)));
        Assert.False(_timers.Start(_door, new string('a', 33), TimeSpan.FromSeconds(5)));
        Assert.Null(_door.Props);
    }

    [Fact]
    public void ASplitStack_LeavesItsTimersWithThePartThatKeepsTheSerial()
    {
        var coins = new ItemEntity { Id = new Serial(0x40000005), TemplateId = "door", ItemId = 0x0EED, Amount = 100 };
        coins.PlaceOnGround(MapType.Trammel, new Point3D(1602, 1600, 0));
        _items.Add([coins]);
        _timers.Start(coins, "melt", TimeSpan.FromSeconds(10));
        coins.SetProp("blessed", true);

        var rest = _items.Split(coins, 40, new Serial(0x40000006));

        Assert.Null(_timers.Remaining(rest, "melt"));
        Assert.True(rest.GetProp<bool>("blessed"));
        Assert.Equal(TimeSpan.FromSeconds(10), _timers.Remaining(coins, "melt"));
    }

    [Fact]
    public void Remaining_OfATimerPropThatIsNotATime_IsNull()
    {
        _door.SetProp("timer.close", "soon");

        Assert.Null(_timers.Remaining(_door, "close"));
    }

    private void Check()
    {
        _wheel.Fire(_wheel.Timers[0].Id);
    }
}
