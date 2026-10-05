using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Death;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Scripting;
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
    private readonly RecordingCrimeService _crimes = new();
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
            crimes: _crimes,
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
    public void Kill_AHumanBody_PlaysItsFallAndItsSound_AndLeavesItsCorpseOnlyWhenTheFallIsOver()
    {
        // ClassicUO undresses a mobile that dies by the death packet: a human body is told to play the fall instead.
        _orc.Body = 0x0190;

        Assert.True(_death.Kill(_orc, _aria));

        Assert.Equal([$"Animated 900 {DeathService.HumanFallAction} {DeathService.HumanFallFrames} 1"], _view.Calls);
        Assert.Single(_speech.Sounds);
        Assert.True(_orc.Frozen);
        Assert.False(_items.TryGet(new Serial(CorpseSerial), out _));
        Assert.Equal(_orc.Id, _sword.MobileId);
        Assert.Empty(_npcs.Removals);
        Assert.Empty(_scripts.Calls);
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((DeathService.FallTime, false), (timer.Interval, timer.Repeat));
        _view.Calls.Clear();

        _timers.Fire(timer.Id);

        // The corpse, with what it wore, then the script and the removal: no death packet.
        Assert.Equal([$"Appeared {CorpseSerial}"], _view.Calls);
        Assert.Equal(new Serial(CorpseSerial), _sword.ContainerId);
        Assert.Equal([$"Run 900 on_death {CorpseSerial} 2"], _scripts.Calls);
        Assert.Equal([_orc.Id], _npcs.Removals);
    }

    [Fact]
    public void Kill_AHumanBodyThatIsFalling_IsRefused_UntilItIsGone()
    {
        _orc.Body = 0x0190;
        _death.Kill(_orc);

        Assert.False(_death.Kill(_orc));

        Assert.Single(_timers.Timers);
        Assert.Single(_view.Calls);
    }

    [Fact]
    public async Task Kill_AHumanBodyRemovedWhileItFalls_LeavesNoCorpse_AndMayDieAgainUnderThatSerial()
    {
        _orc.Body = 0x0190;
        _death.Kill(_orc);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.Delete(_orc.Id));
        _view.Calls.Clear();

        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(_view.Calls);
        Assert.Empty(_scripts.Calls);
        Assert.False(_items.TryGet(new Serial(CorpseSerial), out _));
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_orc));
        Assert.True(_death.Kill(_orc));
    }

    [Fact]
    public void Kill_AMonster_DiesAtOnceByTheDeathPacket_WithNoTimer()
    {
        _death.Kill(_orc);

        Assert.Empty(_timers.Timers);
        Assert.Contains($"MobileDied 900 {CorpseSerial}", _view.Calls);
        Assert.False(_orc.Frozen);
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
    public void Kill_KeepsOnTheCorpseTheNameOfWhoDied_AndWhatItsSpawnRegionGaveIt()
    {
        _orc.SetProp("spawn.region", "felucca_12");
        _orc.SetProp("spawn.x1", 1690);
        _orc.SetProp("mood", "angry");

        _death.Kill(_orc);

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        Assert.Equal(
            ("an orc", "felucca_12", 1690),
            (corpse.GetProp<string>("corpse.name"), corpse.GetProp<string>("corpse.spawn.region"), corpse.GetProp<int>("corpse.spawn.x1"))
        );
        // Only what its region gave it.
        Assert.False(corpse.Props!.ContainsKey("corpse.mood"));
    }

    [Fact]
    public async Task Resurrect_ACorpse_BringsAnNpcOfItsTemplateBack_WithItsNameItsFacingAndItsRegion_AndTheCorpseIsGone()
    {
        _orc.SetProp("spawn.region", "felucca_12");
        // Its home: without the four of them it would wander anywhere.
        foreach (var (key, value) in new[] { ("spawn.x1", 1690), ("spawn.y1", 1691), ("spawn.x2", 1710), ("spawn.y2", 1711) })
        {
            _orc.SetProp(key, value);
        }

        _death.Kill(_orc);
        var born = await BornAsync(0x0011);
        _view.Calls.Clear();

        var result = await _death.ResurrectAsync(new Serial(CorpseSerial));

        Assert.Equal((ResurrectResultType.Raised, born), (result.Type, result.Mobile));
        Assert.Equal(("orc", MapType.Felucca, Spot), Assert.Single(_npcs.Spawns));
        Assert.Equal(("an orc", DirectionType.East, "felucca_12"), (born.Name, born.Direction, born.GetProp<string>("spawn.region")));
        Assert.Equal(
            [1690, 1691, 1710, 1711],
            new[] { "spawn.x1", "spawn.y1", "spawn.x2", "spawn.y2" }.Select(key => born.GetProp<int>(key))
        );
        // Shown again as who it was; a monster does not play a fall backwards.
        Assert.Equal(["MobileAppeared 901", $"Disappeared {CorpseSerial}"], _view.Calls);
        // The corpse and what nobody took: the NPC comes with the things of its template.
        Assert.All(
            new[] { new Serial(CorpseSerial), _gold.Id, _sword.Id },
            serial => Assert.False(_items.TryGet(serial, out _))
        );
    }

    [Fact]
    public async Task Resurrect_AHumanBody_RisesWithItsFallPlayedBackwards()
    {
        _death.Kill(_orc);
        await BornAsync(0x0191);
        _view.Calls.Clear();

        await _death.ResurrectAsync(new Serial(CorpseSerial));

        Assert.Contains(
            $"Animated 901 {DeathService.HumanFallAction} {DeathService.HumanFallFrames} 1 backwards",
            _view.Calls
        );
    }

    [Fact]
    public async Task Resurrect_WhatIsNotACorpseOnTheGround_IsRefused_AndNobodyIsBorn()
    {
        var results = new[]
        {
            // An item that is no corpse, a mobile, nothing at all.
            await _death.ResurrectAsync(_gold.Id),
            await _death.ResurrectAsync(_orc.Id),
            await _death.ResurrectAsync(new Serial(0x40FFFFFF))
        };

        Assert.All(results, result => Assert.Equal((ResurrectResultType.NotACorpse, null), (result.Type, result.Mobile)));
        Assert.Empty(_npcs.Spawns);
    }

    [Theory, InlineData(null), InlineData("gone_template")]
    public async Task Resurrect_ACorpseThatNamesNoTemplateThatExists_IsRefused_AndStaysWhereItIs(string? template)
    {
        _orc.TemplateId = template;
        _death.Kill(_orc);

        var result = await _death.ResurrectAsync(new Serial(CorpseSerial));

        Assert.Equal(ResurrectResultType.CannotBeRaised, result.Type);
        Assert.Empty(_npcs.Spawns);
        Assert.True(_items.TryGet(new Serial(CorpseSerial), out _));
    }

    [Fact]
    public async Task Resurrect_ACorpseSomeoneIsAlreadyBeingRaisedFrom_IsRefused()
    {
        _death.Kill(_orc);
        await BornAsync(0x0011);
        _npcs.Gate = new TaskCompletionSource();
        var first = _death.ResurrectAsync(new Serial(CorpseSerial));

        var second = await _death.ResurrectAsync(new Serial(CorpseSerial));
        _npcs.Gate.SetResult();

        Assert.Equal(ResurrectResultType.CannotBeRaised, second.Type);
        Assert.Equal(ResurrectResultType.Raised, (await first).Type);
        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task Resurrect_WhenTheBirthFails_LeavesTheCorpse_ThatCanBeRaisedAgain()
    {
        _death.Kill(_orc);
        _npcs.SpawnFailure = new InvalidOperationException("no serial left");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _death.ResurrectAsync(new Serial(CorpseSerial)));

        Assert.True(_items.TryGet(new Serial(CorpseSerial), out _));
        _npcs.SpawnFailure = null;
        await BornAsync(0x0011);
        Assert.Equal(ResurrectResultType.Raised, (await _death.ResurrectAsync(new Serial(CorpseSerial))).Type);
    }

    [Fact]
    public async Task Resurrect_ACorpseWhosePropsAScriptSpoiled_StillRaisesWithWhatReads()
    {
        _death.Kill(_orc);
        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        corpse.SetProp("corpse.name", 12);
        corpse.SetProp("corpse.direction", "north");
        var born = await BornAsync(0x0011);
        born.Direction = DirectionType.West;

        var result = await _death.ResurrectAsync(new Serial(CorpseSerial));

        Assert.Equal(ResurrectResultType.Raised, result.Type);
        // Its own name and facing stay.
        Assert.Equal(("an ettin", DirectionType.West), (born.Name, born.Direction));
    }

    [Fact]
    public async Task Resurrect_WhenWhoWasBornIsGoneBeforeItRises_ShowsNothing_AndLeavesTheCorpse()
    {
        // Removed or killed in the turn between its birth and its rising.
        _death.Kill(_orc);
        _npcs.Spawned = new MobileEntity { Id = new Serial(901), Name = "an ettin", TemplateId = "orc", Body = 0x0190 };
        _view.Calls.Clear();

        var result = await _death.ResurrectAsync(new Serial(CorpseSerial));

        Assert.Equal((ResurrectResultType.CannotBeRaised, null), (result.Type, result.Mobile));
        Assert.Empty(_view.Calls);
        Assert.True(_items.TryGet(new Serial(CorpseSerial), out _));
        // The corpse is not left marked.
        await BornAsync(0x0011);
        Assert.Equal(ResurrectResultType.Raised, (await _death.ResurrectAsync(new Serial(CorpseSerial))).Type);
    }

    [Fact]
    public async Task Resurrect_WhenTheCorpseIsGoneBeforeWhoWasBornRises_KeepsWhoWasBorn()
    {
        _death.Kill(_orc);
        var born = await BornAsync(0x0011);
        _npcs.Gate = new TaskCompletionSource();
        var raising = _death.ResurrectAsync(new Serial(CorpseSerial));

        // As by decay, while the NPC is being born.
        _items.Remove([new Serial(CorpseSerial)]);
        _view.Calls.Clear();
        _npcs.Gate.SetResult();

        Assert.Equal(born, (await raising).Mobile);
        Assert.Equal(["MobileAppeared 901"], _view.Calls);
        Assert.Equal("an orc", born.Name);
    }

    [Fact]
    public async Task Resurrect_ACorpseInsideAContainer_IsNotACorpseOnTheGround()
    {
        _death.Kill(_orc);
        Assert.True(_items.TryGet(new Serial(CorpseSerial), out var corpse));
        var chest = new ItemEntity { Id = new Serial(0x40000800), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        chest.PlaceOnGround(MapType.Felucca, Spot);
        _items.Add([chest]);
        _items.MoveToContainer(corpse, chest.Id, new Point2D(10, 10));

        Assert.Equal(ResurrectResultType.NotACorpse, (await _death.ResurrectAsync(corpse.Id)).Type);
    }

    [Fact]
    public void Kill_ACriminal_PardonsIt_SoABodyThatStillFallsIsWantedNoMore()
    {
        // A human body stays in the world while it falls: the next guard would turn on it.
        _orc.Body = 0x0190;
        _orc.Criminal = true;

        _death.Kill(_orc);

        Assert.False(_orc.Criminal);
        Assert.Equal(["pardon 900"], _crimes.Calls);
    }

    [Fact]
    public void Kill_WhoIsNoCriminal_PardonsNobody()
    {
        _death.Kill(_orc);

        Assert.Empty(_crimes.Calls);
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

    // The NPC the next birth gives, in the world as a born one is.
    private async Task<MobileEntity> BornAsync(int body)
    {
        var born = new MobileEntity { Id = new Serial(901), Name = "an ettin", TemplateId = "orc", Body = body };
        _npcs.Spawned = born;
        await _fixture.Network.ExecuteOnLoopAsync(
            () =>
            {
                _fixture.Mobiles.Delete(born.Id);
                born.Map = MapType.Felucca;
                born.Location = Spot;
                _fixture.Mobiles.EnterWorld(born);
            }
        );

        return born;
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
