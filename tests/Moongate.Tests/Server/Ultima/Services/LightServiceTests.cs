using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LightServiceTests : IAsyncLifetime
{
    private static readonly RegionContent Despise = new()
        { Map = MapType.Trammel, Name = "Despise", Type = RegionType.Dungeon };

    private static readonly RegionContent Jail = new() { Map = MapType.Trammel, Name = "Jail", Type = RegionType.Jail };

    private static readonly RegionContent MedusasLair = new()
        { Map = MapType.Trammel, Name = "Medusas Lair", Parent = "Despise" };

    private readonly StubClockService _clock = new();
    private readonly RecordingTimerService _timers = new();
    private readonly WorldConfig _world = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingItemScriptService _itemScripts = new() { Scripted = { "decoration_light" } };
    private readonly ItemEntity _lampPost = Light(0x40000001, 0x0B21, "LampPost1");
    private BroadcastFixture _fixture = null!;
    private LightService _light = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _light = new(
            _clock,
            _fixture.Sessions,
            _fixture.Mobiles,
            _fixture.Sender,
            _timers,
            _fixture.Network.Loop,
            _world,
            new StubDataLoaderService().With(Despise, Jail, MedusasLair),
            _items,
            _itemScripts
        );
        _items.Add([_lampPost, Light(0x40000002, 0x0A28, "Candle")]);
        await _light.StartAsync();
    }

    [Theory,
     InlineData(0, 0, 12), InlineData(3, 59, 12), InlineData(4, 0, 12), InlineData(5, 0, 6), InlineData(6, 0, 0),
     InlineData(12, 0, 0), InlineData(21, 59, 0), InlineData(22, 0, 0), InlineData(23, 0, 6), InlineData(23, 59, 11)]
    public void LevelFor_FollowsModernUOsBands(int hours, int minutes, int level)
    {
        _clock.Time = new GameTime(hours, minutes);

        Assert.Equal(level, _light.LevelFor(Mobile()));
    }

    [Fact]
    public void LevelFor_UsesTheConfiguredLevels()
    {
        _world.DayLight = 2;
        _world.NightLight = 20;

        _clock.Time = new GameTime(1, 0);
        Assert.Equal(20, _light.LevelFor(Mobile()));
        _clock.Time = new GameTime(5, 0);
        Assert.Equal(11, _light.LevelFor(Mobile()));
    }

    [Fact]
    public async Task Tick_SendsTheLevelToThePlayersInTheWorld_OnlyWhenItChanges()
    {
        await LoginAsync(1);
        await _fixture.AddAsync(2, entered: false);
        _clock.Time = new GameTime(23, 0);
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((LightService.TimerName, TimeSpan.FromSeconds(5), true), (timer.Name, timer.Interval, timer.Repeat));

        _timers.Fire(timer.Id);
        _timers.Fire(timer.Id);

        Assert.Equal([(1L, 6)], Sent());

        _clock.Time = new GameTime(1, 0);
        _timers.Fire(timer.Id);

        Assert.Equal([(1L, 6), (1L, 12)], Sent());
    }

    [Fact]
    public async Task Tick_SkipsACharacterInTheWorldBeforeItsLoginSequenceSentItsLight()
    {
        // In the world, but the login has not sent 0x1B yet: a 0x4F now would reach the client too early.
        await _fixture.AddAsync(1);

        _timers.Fire(Assert.Single(_timers.Timers).Id);
        await _light.SetOverrideAsync(25);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task LevelOnLogin_CountsAsSent()
    {
        await _fixture.AddAsync(1);
        _fixture.Mobiles.TryGet(new Serial(1), out var character);

        Assert.Equal(0, _light.LevelOnLogin(character!));
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task SetOverrideAsync_SendsTheLevelAtOnce_AndClearingItGoesBackToTheClock()
    {
        await LoginAsync(1);
        _clock.Time = new GameTime(1, 0);
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        await _light.SetOverrideAsync(25);
        Assert.Equal((25, 25), (_light.Override, _light.LevelFor(Mobile())));
        await _light.SetOverrideAsync(null);

        Assert.Null(_light.Override);
        Assert.Equal([(1L, 12), (1L, 25), (1L, 12)], Sent());
    }

    [Fact]
    public async Task SetOverride_OnTheLoop_SendsTheLevelAtOnce_WithoutWaiting()
    {
        await LoginAsync(1);
        _clock.Time = new GameTime(1, 0);
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        _light.SetOverride(25);
        Assert.Equal((25, 25), (_light.Override, _light.LevelFor(Mobile())));
        _light.SetOverride(null);

        Assert.Null(_light.Override);
        Assert.Equal([(1L, 12), (1L, 25), (1L, 12)], Sent());
    }

    [Fact]
    public void LevelFor_ADungeonIsDark_AndAJailDim_WhateverTheTime()
    {
        var mobile = Mobile();

        _light.RegionChanged(mobile, null, Despise);
        Assert.Equal(26, _light.LevelFor(mobile));

        _light.RegionChanged(mobile, Despise, Jail);
        Assert.Equal(9, _light.LevelFor(mobile));

        _light.Left(mobile.Id);
        Assert.Equal(0, _light.LevelFor(mobile));
    }

    [Fact]
    public void LevelFor_APlainChildOfADungeon_IsDarkToo()
    {
        var mobile = Mobile();

        _light.RegionChanged(mobile, Despise, MedusasLair);

        Assert.Equal(26, _light.LevelFor(mobile));
    }

    [Fact]
    public async Task LevelFor_TheOverrideWinsEvenInADungeon()
    {
        var mobile = Mobile();
        _light.RegionChanged(mobile, null, Despise);

        await _light.SetOverrideAsync(0);

        Assert.Equal(0, _light.LevelFor(mobile));
    }

    [Fact]
    public async Task ARegionChange_SendsTheNewLevelAtOnce_OnlyAfterTheLogin()
    {
        await _fixture.AddAsync(2);
        _fixture.Mobiles.TryGet(new Serial(2), out var early);
        _light.RegionChanged(early!, null, Despise);
        Assert.Empty(Sent());

        await LoginAsync(1);
        _fixture.Mobiles.TryGet(new Serial(1), out var character);
        _light.RegionChanged(character!, null, Despise);
        _light.RegionChanged(character!, Despise, Despise);

        Assert.Equal([(1L, 26)], Sent());
    }

    [Fact]
    public void LampPosts_AreToldWhenItGetsDarkOrLight_EveryThirtySeconds_OnlyOnAChange()
    {
        var timer = Assert.Single(_timers.Timers).Id;

        FireChecks(timer, 5);
        Assert.Empty(_itemScripts.Queued);

        FireChecks(timer, 1);
        Assert.Equal(["0x40000001 on_darkness False"], _itemScripts.Queued);

        _clock.Time = new GameTime(1, 0);
        FireChecks(timer, 6);
        FireChecks(timer, 6);

        Assert.Equal(["0x40000001 on_darkness False", "0x40000001 on_darkness True"], _itemScripts.Queued);
    }

    [Fact]
    public void LampPosts_TurnOnAtTheConfiguredLevel()
    {
        _world.LampPostLight = 10;
        _clock.Time = new GameTime(23, 0);
        FireChecks(Assert.Single(_timers.Timers).Id, 6);

        Assert.Equal(["0x40000001 on_darkness False"], _itemScripts.Queued);
    }

    [Fact]
    public async Task LampPosts_FollowTheOverrideAtOnce()
    {
        await _light.SetOverrideAsync(26);

        Assert.Equal(["0x40000001 on_darkness True"], _itemScripts.Queued);
    }

    [Fact]
    public async Task StopAsync_RemovesTheTimer()
    {
        await _light.StopAsync();

        Assert.Empty(_timers.Timers);
    }

    private async Task LoginAsync(long id)
    {
        await _fixture.AddAsync(id);
        _fixture.Mobiles.TryGet(new Serial((uint)id), out var character);
        _light.LevelOnLogin(character!);
    }

    private List<(long, int)> Sent()
    {
        return _fixture.Sender.Sent
            .Select((packet, index) => (packet, index))
            .Where(pair => pair.packet is GlobalLightLevelPacket)
            .Select(pair => (_fixture.Sender.SentSessionIds[pair.index], ((GlobalLightLevelPacket)pair.packet).Level))
            .ToList();
    }

    private void FireChecks(string timer, int count)
    {
        for (var check = 0; check < count; check++)
        {
            _timers.Fire(timer);
        }
    }

    private static ItemEntity Light(uint serial, int graphic, string type)
    {
        var light = new ItemEntity
        {
            Id = new Serial(serial), TemplateId = "decoration_light", ItemId = graphic, Amount = 1,
            Props = new() { ["decoration_type"] = type }
        };
        light.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));

        return light;
    }

    private static MobileEntity Mobile()
    {
        return new() { Id = new Serial(9), Map = MapType.Trammel };
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
