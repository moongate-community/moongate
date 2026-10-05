using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Moongate.Core.Types.Geometry;
using Serilog;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class DeathServiceTests : IAsyncLifetime
{
    private const uint CorpseSerial = 0x40000900;

    private static readonly Point3D Spot = new(1700, 1700, 5);

    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubNpcService _npcs = new();
    private readonly RecordingNpcScriptService _scripts = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly CapturingLogSink _log = new();
    private readonly FakeScriptEngine _engine = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly SettableClock _clock = new();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                 .Item(0x0EED, TileFlagType.Generic, 0)
                                                 .Item(0x0E75, TileFlagType.Container, 0)
                                                 .Item(0x2006, TileFlagType.Container, 0);
    private readonly ItemTemplateService _itemTemplates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "corpse", ItemId = new Serial(0x2006), Movable = false },
            new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
            new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
            new ItemTemplate { Id = "sword", ItemId = new Serial(0x0F5E) },
            new ItemTemplate { Id = "hair", ItemId = new Serial(0x203B) },
            new ItemTemplate { Id = "newbie_dagger", ItemId = new Serial(0x0F52), LootType = LootType.Newbied },
            new ItemTemplate { Id = "blessed_ring", ItemId = new Serial(0x108A), LootType = LootType.Blessed },
            new ItemTemplate { Id = "statue", ItemId = new Serial(0x1224), Movable = false }
        )
    );
    private readonly MobileTemplateService _mobileTemplates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "orc", Sounds = new() { Death = 0x01B2 } },
            new MobileTemplate { Id = "mute" }
        )
    );

    private uint _nextItem = 0x40000001;
    private BroadcastFixture _fixture = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _orc = null!;
    private ItemEntity _backpack = null!;
    private ItemEntity _gold = null!;
    private ItemEntity _sword = null!;
    private DeathService _death = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.Name = "Aria";
        _aria.AccountId = new Serial(1002);
        _orc = new()
        {
            Id = new Serial(900), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca, Location = Spot, Body = 0x0011,
            SkinHue = new Hue(0x0021), Direction = DirectionType.East
        };
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_orc));
        _backpack = Worn(_orc, "backpack", 0x0E75, LayerType.Backpack);
        _gold = Carried(_backpack, "gold", 0x0EED, 20);
        _sword = Worn(_orc, "sword", 0x0F5E, LayerType.OneHanded);
        _serials.Serials.Enqueue(new Serial(CorpseSerial));
        _death = new(
            _fixture.Mobiles,
            _items,
            new ItemHandlingService(
                _items,
                _fixture.Sessions,
                _fixture.Sender,
                _view,
                TestTooltips.Create(_items, _fixture.Mobiles),
                new FakeItemFactoryService(_itemTemplates, _tiles),
                _serials
            ),
            _view,
            _speech,
            _npcs,
            _scripts,
            _mobileTemplates,
            _itemTemplates,
            _loop,
            new Lazy<IScriptEngine>(() => _engine),
            _timers,
            _clock,
            logger: new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(_log).CreateLogger()
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Kill_AnNpc_LeavesItsCorpseWhereItStood_WithItsBodyItsFacingItsHueAndItsName()
    {
        Assert.True(_death.Kill(_orc));

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.Equal(
            ("corpse", 0x2006, 1, "the remains of an orc", (ushort)0x0021),
            (corpse.TemplateId, corpse.ItemId, corpse.Amount, corpse.Name, corpse.Hue.Value)
        );
        Assert.Equal((MapType.Felucca, Spot), (corpse.Map, corpse.GroundLocation));
        Assert.Equal(
            (0x0011, (int)DirectionType.East, "orc"),
            (corpse.GetProp<int>("corpse.body"), corpse.GetProp<int>("corpse.direction"), corpse.GetProp<string>("corpse.template"))
        );
        Assert.False(corpse.TryGetProp<long>("corpse.killer", out _));
    }

    [Fact]
    public void Kill_PutsWhatTheNpcWoreAndWhatLayInItsBackpackIntoTheCorpse()
    {
        _death.Kill(_orc);

        Assert.Equal(
            [_gold.Id, _sword.Id],
            _items.GetContents(new Serial(CorpseSerial)).Select(item => item.Id).Order()
        );
        Assert.Equal((null, null), (_sword.MobileId, _sword.Layer));
        Assert.Equal(20, _gold.Amount);
    }

    [Fact]
    public void Kill_KeepsOnTheCorpseWhatTheNpcWoreAndOnWhichLayer_NotWhatLayInItsBackpack()
    {
        var shirt = Worn(_orc, "sword", 0x1517, LayerType.Shirt);

        _death.Kill(_orc);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.Equal(
            new[] { $"{_sword.Id.Value}:{(int)LayerType.OneHanded}", $"{shirt.Id.Value}:{(int)LayerType.Shirt}" }.Order(),
            corpse.GetProp<string>("corpse.worn").Split(',').Order()
        );
    }

    [Fact]
    public void Kill_AHumanBody_HoldsTheDressOfItsCorpseUntilTheFallIsOver_ThenShowsItAgain()
    {
        // The client takes what a corpse wears off the mobile: told at once, it would fall naked.
        _orc.Body = 0x0190;

        _death.Kill(_orc);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.Equal(
            (_clock.Now + DeathService.FallTime).ToUnixTimeMilliseconds(),
            corpse.GetProp<long>("corpse.dress_at")
        );
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((DeathService.FallTime, false), (timer.Interval, timer.Repeat));
        _view.Calls.Clear();

        _timers.Fire(timer.Id);

        Assert.False(corpse.Props!.ContainsKey("corpse.dress_at"));
        Assert.Equal([$"Appeared {CorpseSerial}"], _view.Calls);
    }

    [Fact]
    public void Kill_AHumanBody_WhoseCorpseIsGoneBeforeTheFallIsOver_ShowsNothingAgain()
    {
        _orc.Body = 0x0190;
        _death.Kill(_orc);
        _items.Remove([new Serial(CorpseSerial)]);
        _view.Calls.Clear();

        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void Kill_AMonster_OrAHumanBodyWithNothingToWear_HoldsNothing()
    {
        _death.Kill(_orc);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.False(corpse.Props!.ContainsKey("corpse.dress_at"));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Kill_KeepsOnTheCorpseTheHairAndTheBeardOfWhoDied()
    {
        _orc.HairStyle = 0x203B;
        _orc.HairHue = new Hue(0x0455);
        _orc.BeardStyle = 0x203E;
        _orc.BeardHue = new Hue(0x0456);

        _death.Kill(_orc);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.Equal(
            (0x203B, 0x0455, 0x203E, 0x0456),
            (corpse.GetProp<int>("corpse.hair"), corpse.GetProp<int>("corpse.hair_hue"), corpse.GetProp<int>("corpse.beard"), corpse.GetProp<int>("corpse.beard_hue"))
        );
    }

    [Fact]
    public void Kill_ABaldNakedNpc_KeepsNoneOfThatOnTheCorpse()
    {
        _items.Remove([_sword.Id]);

        _death.Kill(_orc);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.All(
            new[] { "corpse.worn", "corpse.hair", "corpse.hair_hue", "corpse.beard", "corpse.beard_hue" },
            key => Assert.False(corpse.Props!.ContainsKey(key))
        );
    }

    [Fact]
    public void Kill_LeavesOnTheNpcItsBackpackItsHairAndWhatDoesNotDrop()
    {
        var hair = Worn(_orc, "hair", 0x203B, LayerType.Hair);
        var ring = Worn(_orc, "blessed_ring", 0x108A, LayerType.Ring);
        var dagger = Carried(_backpack, "newbie_dagger", 0x0F52, 1);
        var statue = Carried(_backpack, "statue", 0x1224, 1);
        var nailed = Carried(_backpack, "gold", 0x0EED, 5);
        nailed.Movable = false;
        var bank = Worn(_orc, "backpack", 0x0E75, LayerType.Bank);

        _death.Kill(_orc);

        // They go with the NPC when it is removed.
        Assert.All(
            new[] { _backpack, hair, ring, bank },
            item => Assert.Equal(_orc.Id, item.MobileId)
        );
        Assert.All(new[] { dagger, statue, nailed }, item => Assert.Equal(_backpack.Id, item.ContainerId));
    }

    [Fact]
    public void Kill_ShowsTheCorpseThenTheDeath_TellsTheScript_AndRemovesTheNpc()
    {
        _death.Kill(_orc, _aria);

        Assert.Equal([$"Appeared {CorpseSerial}", $"MobileDied 900 {CorpseSerial}"], _view.Calls);
        Assert.Equal([$"Run 900 on_death {CorpseSerial} 2"], _scripts.Calls);
        Assert.Equal([_orc.Id], _npcs.Removals);
    }

    [Fact]
    public void Kill_TheCorpseIsFilledBeforeItIsShown_AndTheScriptRunsBeforeTheNpcIsRemoved()
    {
        var contentsWhenShown = -1;
        var removalsWhenDied = -1;
        _view.OnCall = call =>
        {
            if (call.StartsWith("Appeared", StringComparison.Ordinal))
            {
                contentsWhenShown = _items.GetContents(new Serial(CorpseSerial)).Count;
            }
            else if (call.StartsWith("MobileDied", StringComparison.Ordinal))
            {
                removalsWhenDied = _npcs.Removals.Count;
            }
        };

        _death.Kill(_orc);

        Assert.Equal((2, 0), (contentsWhenShown, removalsWhenDied));
    }

    [Fact]
    public void Kill_ByAKiller_KeepsWhoItWasOnTheCorpse()
    {
        _death.Kill(_orc, _aria);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.Equal(2L, corpse.GetProp<long>("corpse.killer"));
    }

    [Fact]
    public void Kill_PlaysTheDeathSoundOfTheTemplate()
    {
        _death.Kill(_orc);

        Assert.Equal((_orc, 0x01B2), Assert.Single(_speech.Sounds));
    }

    [Theory]
    [InlineData(0x0190, GenderType.Male, 0x15A)]
    [InlineData(0x0191, GenderType.Female, 0x150)]
    [InlineData(0x025D, GenderType.Male, 0x15A)]
    [InlineData(0x029B, GenderType.Female, 0x150)]
    public void Kill_AHumanBodyWithoutASoundOfItsOwn_DiesWithOneOfTheFourVoicesOfItsGender(int body, GenderType gender, int first)
    {
        _orc.TemplateId = "mute";
        _orc.Body = body;
        _orc.Gender = gender;

        _death.Kill(_orc);

        Assert.InRange(Assert.Single(_speech.Sounds).Sound, first, first + 3);
    }

    [Fact]
    public void Kill_AnotherBodyWithoutASound_DiesInSilence()
    {
        _orc.TemplateId = "mute";

        Assert.True(_death.Kill(_orc));

        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void Kill_WhenNoCorpseCanBeMade_TheNpcDiesAllTheSame_WithoutOne()
    {
        _serials.Serials.Clear();

        Assert.True(_death.Kill(_orc, _aria));

        Assert.Equal(["MobileDied 900 0"], _view.Calls);
        Assert.Equal(["Run 900 on_death  2"], _scripts.Calls);
        Assert.Equal([_orc.Id], _npcs.Removals);
        // What it carried stays on it and goes with it.
        Assert.Equal(_backpack.Id, _gold.ContainerId);
    }

    [Fact]
    public void Kill_FromInsideARunningScript_LeavesTheScriptAndTheRemovalToTheNextTurnOfTheLoop()
    {
        // The engine refuses a script started inside another: on_death cannot run nested in the one that kills.
        _engine.IsRunningScript = true;
        _loop.DeferTryPost = true;

        Assert.True(_death.Kill(_orc, _aria));

        // The death is seen at once.
        Assert.Equal([$"Appeared {CorpseSerial}", $"MobileDied 900 {CorpseSerial}"], _view.Calls);
        Assert.Equal([$"Queue 900 on_death {CorpseSerial} 2"], _scripts.Calls);
        Assert.Empty(_npcs.Removals);

        _loop.RunDeferred();

        Assert.Equal([_orc.Id], _npcs.Removals);
    }

    [Fact]
    public void Kill_AnNpcThatIsAlreadyDying_IsRefused_AndMakesNoSecondCorpse()
    {
        _engine.IsRunningScript = true;
        _loop.DeferTryPost = true;
        _serials.Serials.Enqueue(new Serial(CorpseSerial + 1));
        _death.Kill(_orc);

        Assert.False(_death.Kill(_orc));

        Assert.False(_items.TryGet(new Serial(CorpseSerial + 1), out _));
        Assert.Single(_view.Calls, call => call.StartsWith("MobileDied", StringComparison.Ordinal));

        // Once it is gone it may be told to die again, as a serial used anew.
        _loop.RunDeferred();
        Assert.True(_death.Kill(_orc));
    }

    [Fact]
    public void Kill_WhoseScriptFails_RemovesTheNpcAllTheSame()
    {
        _scripts.Throws = new InvalidOperationException("boom");

        Assert.True(_death.Kill(_orc));

        Assert.Equal([_orc.Id], _npcs.Removals);
    }

    [Fact]
    public void Kill_AnItemWhoseOwnFlagSaysItMoves_DropsIt_WhateverItsTemplate()
    {
        var statue = Carried(_backpack, "statue", 0x1224, 1);
        statue.Movable = true;

        _death.Kill(_orc);

        Assert.Equal(new Serial(CorpseSerial), statue.ContainerId);
    }

    [Fact]
    public void Kill_APlayer_IsRefused_AndNothingHappens()
    {
        Assert.False(_death.Kill(_aria));

        Assert.Empty(_view.Calls);
        Assert.Empty(_npcs.Removals);
        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public async Task Kill_AnNpcThatIsNotInTheWorld_IsRefused()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.Delete(_orc.Id));

        Assert.False(_death.Kill(_orc));

        Assert.Empty(_view.Calls);
        Assert.False(_items.TryGet(new Serial(CorpseSerial), out _));
    }

    [Fact]
    public void Kill_SaysInTheLogWhoDiedWhereAndByWhom()
    {
        _death.Kill(_orc, _aria);

        var line = Assert.Single(_log.Events).RenderMessage();
        Assert.Equal("an orc (0x00000384) died at (1700, 1700, 5) of Felucca, killed by Aria", line);
    }

    private ItemEntity Worn(MobileEntity owner, string template, int graphic, LayerType layer)
    {
        var item = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = template, ItemId = graphic, Amount = 1 };
        item.Equip(owner.Id, layer);
        _items.Add([item]);

        return item;
    }

    private ItemEntity Carried(ItemEntity container, string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(container.Id, new Point2D(44, 65));
        _items.Add([item]);

        return item;
    }
}
