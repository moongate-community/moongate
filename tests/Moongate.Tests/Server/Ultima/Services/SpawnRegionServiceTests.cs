using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Spawns;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Random;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SpawnRegionServiceTests : IAsyncLifetime
{
    private readonly RecordingTimerService _timers = new();
    private readonly FakeMapService _map = new(200, 200);
    private readonly StubMovementService _movement = new() { LandingZ = 5 };
    private readonly StubNpcService _npcs = new();
    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;
    private SpawnRegionService _service = null!;
    private uint _nextSerial = 0x1000;

    // Most tests follow the spawn rules after the first fill.
    private bool _initialFill;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
    }

    [Fact]
    public async Task TheFirstSpawnAfterTheStart_FillsTheRegionToItsMax_ThenItGoesByCall()
    {
        _initialFill = true;
        await StartAsync(new ScriptedRandom(0), Spawn("forest", call: 1, max: 5, minMinutes: 5, maxMinutes: 5));
        await AddLiveAsync("forest");

        await TickAsync();
        Assert.Equal(4, _npcs.Spawns.Count);

        _clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync();
        Assert.Equal(5, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task FillAll_FillsEveryRegionToItsMaxAtTheNextCheck_AndTellsWhatIsMissing()
    {
        // 600 seconds: the regions' own first spawn is far away.
        await StartAsync(
            new ScriptedRandom(600),
            Spawn("forest", call: 1, max: 5, minMinutes: 30, maxMinutes: 30),
            Spawn("glade", call: 1, max: 3, minMinutes: 30, maxMinutes: 30)
        );
        await AddLiveAsync("forest");

        var (regions, missing) = await _service.FillAllAsync();
        await TickAsync();

        Assert.Equal((2, 7), (regions, missing));
        Assert.Equal(7, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task WithoutTheInitialFill_TheFirstSpawnGoesByCall()
    {
        await StartAsync(new ScriptedRandom(0), Spawn("forest", call: 1, max: 5));

        await TickAsync();

        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task TheFirstSpawn_ComesWithinTheMinTime()
    {
        // 300 seconds of the 10 minutes.
        await StartAsync(new ScriptedRandom(300), Spawn("forest", minMinutes: 10, maxMinutes: 20));

        await TickAsync();
        Assert.Empty(_npcs.Spawns);

        _clock.Advance(TimeSpan.FromSeconds(300));
        await TickAsync();
        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task TheFirstSpawn_ComesWithinTenMinutesEvenForALongMinTime()
    {
        await StartAsync(new ScriptedRandom(int.MaxValue), Spawn("bank", minMinutes: 480, maxMinutes: 480));

        _clock.Advance(TimeSpan.FromMinutes(10));
        await TickAsync();

        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task ADueRegion_SpawnsCallNpcs_WhereTheMovementPlacesThem()
    {
        _movement.SpawnZ = (_, _) => 7;
        await StartAsync(new ScriptedRandom(0), Spawn("forest", call: 2, max: 5, x1: 10, y1: 20, x2: 10, y2: 20));

        await TickAsync();

        Assert.Equal(
            [("rabbit", MapType.Felucca, new Point3D(10, 20, 7)), ("rabbit", MapType.Felucca, new Point3D(10, 20, 7))],
            _npcs.Spawns
        );
    }

    [Fact]
    public async Task TheLiveNpcsOfARegion_CountAgainstItsMax()
    {
        await StartAsync(new ScriptedRandom(0), Spawn("forest", call: 3, max: 4));
        await AddLiveAsync("forest");
        await AddLiveAsync("forest");
        await AddLiveAsync("elsewhere");

        await TickAsync();

        Assert.Equal(2, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task AFullRegion_SpawnsNothing_UntilOneOfItsNpcsIsGone()
    {
        await StartAsync(new ScriptedRandom(0), Spawn("forest", max: 1, minMinutes: 5, maxMinutes: 5));
        var live = await AddLiveAsync("forest");

        await TickAsync();
        Assert.Empty(_npcs.Spawns);

        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.Delete(live.Id));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync();
        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task TheNextSpawn_ComesBetweenTheMinAndMaxTime()
    {
        // The first spawn at once, then the next roll: 90 seconds over the 5 minutes.
        await StartAsync(new ScriptedRandom(0, 0, 0, 0, 0, 390), Spawn("forest", max: 5, minMinutes: 5, maxMinutes: 10));
        await TickAsync();

        _clock.Advance(TimeSpan.FromSeconds(389));
        await TickAsync();
        Assert.Single(_npcs.Spawns);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await TickAsync();
        Assert.Equal(2, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task ASpotInAnExcludedArea_IsNeverTaken()
    {
        var spawn = Spawn("forest", call: 10, max: 10, x1: 10, y1: 10, x2: 14, y2: 14);
        spawn.Exclude = [new() { X1 = 10, Y1 = 10, X2 = 13, Y2 = 14 }];
        await StartAsync(new System.Random(1), spawn);

        await TickAsync();

        Assert.Equal(10, _npcs.Spawns.Count);
        Assert.All(_npcs.Spawns, spawned => Assert.Equal(14, spawned.Location.X));
    }

    [Fact]
    public async Task NoSpot_RetriesAMinuteLater()
    {
        var tries = 0;
        _movement.SpawnZ = (_, _) => ++tries > 100 ? 0 : null;
        await StartAsync(new ScriptedRandom(0), Spawn("forest", minMinutes: 30, maxMinutes: 30));

        await TickAsync();
        Assert.Empty(_npcs.Spawns);
        Assert.Equal(100, tries);

        _clock.Advance(TimeSpan.FromSeconds(59));
        await TickAsync();
        Assert.Equal(100, tries);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await TickAsync();
        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task OnlyOutside_RefusesASpotUnderARoof()
    {
        _map.AddStatic(10, 10, 0x0519, 30);
        var inside = Spawn("house", x1: 10, y1: 10, x2: 10, y2: 10);
        inside.OnlyOutside = true;
        var anywhere = Spawn("hall", x1: 10, y1: 10, x2: 10, y2: 10);
        await StartAsync(new ScriptedRandom(0), inside, anywhere);

        await TickAsync();

        Assert.Single(_npcs.Spawns);
        Assert.Equal("hall", _npcs.Spawned.GetProp<string>(SpawnRegionService.RegionProp));
    }

    [Fact]
    public async Task TheSpotsCeiling_IsTheGroundPlusPrefZ_OrTheFixedZ()
    {
        var preferred = Spawn("preferred");
        preferred.PrefZ = 7;
        var fixedZ = Spawn("fixed");
        fixedZ.Z = 30;
        await StartAsync(new ScriptedRandom(0), Spawn("default"), preferred, fixedZ);

        await TickAsync();

        Assert.Equal([23, 12, 30], _movement.SpawnCeilings);
    }

    [Fact]
    public async Task ASpawnedNpc_KeepsItsRegionAndHomeArea()
    {
        await StartAsync(new ScriptedRandom(0), Spawn("forest", x1: 10, y1: 20, x2: 30, y2: 40));

        await TickAsync();

        var npc = _npcs.Spawned;
        Assert.Equal("forest", npc.GetProp<string>(SpawnRegionService.RegionProp));
        Assert.Equal(
            [10L, 20L, 30L, 40L],
            new[] { "spawn.x1", "spawn.y1", "spawn.x2", "spawn.y2" }.Select(key => npc.GetProp<long>(key))
        );
    }

    [Fact]
    public async Task ARegionOnAMapNotLoaded_NeverSpawns()
    {
        var trammel = Spawn("trammel_forest");
        trammel.Map = MapType.Trammel;
        await StartAsync(new ScriptedRandom(0), trammel);

        await TickAsync();

        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public async Task AFailedSpawn_DoesNotStopTheOthers()
    {
        _npcs.SpawnFailure = new InvalidOperationException("boom");
        await StartAsync(new ScriptedRandom(0), Spawn("forest", call: 2, max: 2));

        await TickAsync();
        _npcs.SpawnFailure = null;
        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();

        Assert.Equal(4, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task TheStaff_HearOneRegionsSpawn()
    {
        var staff = await AddPlayerAsync(1, AccountType.GameMaster);
        await AddPlayerAsync(2, AccountType.Regular);
        await StartAsync(new ScriptedRandom(0), Spawn("forest", name: "Yew Woods", call: 2, max: 2));

        await TickAsync();

        Assert.Equal([(staff, "Spawn: Yew Woods (Felucca): 2 NPCs - world 0/2 (0%)")], Notices());
    }

    [Fact]
    public async Task TheStaff_HearHowFullTheWorldIs()
    {
        var staff = await AddPlayerAsync(1, AccountType.GameMaster);
        await StartAsync(new ScriptedRandom(0), Spawn("forest", name: "Yew Woods", call: 1, max: 4), Spawn("glade", max: 4));
        await AddLiveAsync("forest");
        await AddLiveAsync("glade");
        await AddLiveAsync("glade");

        await TickAsync();

        Assert.Equal((staff, "Spawn: 2 NPCs in 2 regions: Yew Woods 1, glade 1 - world 3/8 (37%)"), Assert.Single(Notices()));
    }

    [Fact]
    public async Task TheStaff_HearAtMostFiveRegionsByName()
    {
        await AddPlayerAsync(1, AccountType.Administrator);
        var spawns = Enumerable.Range(1, 7).Select(index => Spawn($"r{index}", name: $"R{index}", call: index == 7 ? 2 : 1, max: 2));
        await StartAsync(new ScriptedRandom(0), spawns.ToArray());

        await TickAsync();

        Assert.Equal("Spawn: 8 NPCs in 7 regions: R7 2, R1 1, R2 1, R3 1, R4 1 and 2 more - world 0/14 (0%)", Assert.Single(Notices()).Text);
    }

    [Fact]
    public async Task ASeaCreature_SpawnsOnTheWater()
    {
        _movement.SwimZ = (_, _) => -5;
        var ocean = Spawn("ocean", x1: 10, y1: 20, x2: 10, y2: 20);
        ocean.MobileIds = ["dolphin"];
        await StartAsync(new ScriptedRandom(0), ocean);

        await TickAsync();

        Assert.Equal([("dolphin", MapType.Felucca, new Point3D(10, 20, -5))], _npcs.Spawns);
        Assert.Empty(_movement.SpawnCeilings);
    }

    [Fact]
    public async Task ASeaCreature_WithNoWater_RetriesAMinuteLater()
    {
        var ocean = Spawn("ocean", minMinutes: 30, maxMinutes: 30);
        ocean.MobileIds = ["dolphin"];
        await StartAsync(new ScriptedRandom(0), ocean);

        await TickAsync();
        Assert.Empty(_npcs.Spawns);

        _movement.SwimZ = (_, _) => -5;
        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();
        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task ASeaCreatureWithNoWater_DoesNotStopTheLandNpcsOfTheSameCall()
    {
        // Rolls: the first spawn at once, then dolphin (0) with no water in 100 tries, then rabbit (1) on land.
        var mixed = Spawn("coast", call: 2, max: 2, x1: 10, y1: 10, x2: 10, y2: 10);
        mixed.MobileIds = ["dolphin", "rabbit"];
        await StartAsync(new ScriptedRandom([0, 0, .. Enumerable.Repeat(0, 300), 1]), mixed);

        await TickAsync();

        Assert.Equal(["rabbit"], _npcs.Spawns.Select(spawn => spawn.TemplateId));
    }

    [Fact]
    public async Task OnlyOutside_RefusesWaterUnderARoof()
    {
        _movement.SwimZ = (_, _) => -5;
        _map.AddStatic(10, 10, 0x0519, 20);
        var pier = Spawn("pier", x1: 10, y1: 10, x2: 10, y2: 10);
        pier.MobileIds = ["dolphin"];
        pier.OnlyOutside = true;
        await StartAsync(new ScriptedRandom(0), pier);

        await TickAsync();

        Assert.Empty(_npcs.Spawns);
    }

    [Theory, InlineData(0, 0), InlineData(null, -5)]
    public async Task AnAmphibian_SpawnsOnLand_ElseOnTheWater(int? land, int expectedZ)
    {
        _movement.SpawnZ = (_, _) => land;
        _movement.SwimZ = (_, _) => -5;
        var shore = Spawn("shore", x1: 10, y1: 20, x2: 10, y2: 20);
        shore.MobileIds = ["walrus"];
        await StartAsync(new ScriptedRandom(0), shore);

        await TickAsync();

        Assert.Equal([("walrus", MapType.Felucca, new Point3D(10, 20, expectedZ))], _npcs.Spawns);
    }

    [Fact]
    public async Task RegionsAtAsync_GivesTheRegionsHere_WithTheirLiveNpcsAndNextSpawn()
    {
        // 120 seconds of the 5 minutes.
        await StartAsync(
            new ScriptedRandom(120),
            Spawn("forest", name: "Yew Woods", max: 4, minMinutes: 5, maxMinutes: 5, x1: 10, y1: 10, x2: 20, y2: 20),
            Spawn("glade", max: 2, minMinutes: 5, maxMinutes: 5, x1: 15, y1: 15, x2: 30, y2: 30),
            Spawn("elsewhere", x1: 100, y1: 100, x2: 110, y2: 110)
        );
        await AddLiveAsync("forest");
        _clock.Advance(TimeSpan.FromSeconds(30));

        var here = await _service.RegionsAtAsync(MapType.Felucca, 16, 16);

        Assert.Equal(
            [new("forest", "Yew Woods", 1, 4, TimeSpan.FromSeconds(90), false), new SpawnRegionStatus("glade", null, 0, 2, TimeSpan.FromSeconds(90), false)],
            here
        );
        Assert.Empty(await _service.RegionsAtAsync(MapType.Trammel, 16, 16));
    }

    [Fact]
    public async Task RegionsAtAsync_ARegionWithNoSpot_IsRetrying_UntilItSpawns()
    {
        var tries = 0;
        _movement.SpawnZ = (_, _) => ++tries > 100 ? 0 : null;
        await StartAsync(new ScriptedRandom(0), Spawn("forest", minMinutes: 30, maxMinutes: 30));

        await TickAsync();
        Assert.True(Assert.Single(await _service.RegionsAtAsync(MapType.Felucca, 10, 10)).Retrying);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();
        Assert.False(Assert.Single(await _service.RegionsAtAsync(MapType.Felucca, 10, 10)).Retrying);
    }

    [Fact]
    public async Task RegionsAtAsync_AnOverdueSpawn_IsDueNow()
    {
        await StartAsync(new ScriptedRandom(0), Spawn("forest"));
        _clock.Advance(TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.Zero, Assert.Single(await _service.RegionsAtAsync(MapType.Felucca, 10, 10)).NextSpawnIn);
    }

    [Fact]
    public async Task StopAsync_CancelsTheSpawnsStillToCome()
    {
        _npcs.Gate = new();
        await StartAsync(new ScriptedRandom(0), Spawn("forest", call: 3, max: 3));
        await _fixture.Network.ExecuteOnLoopAsync(() => _timers.Fire(TimerId()));

        await _service.StopAsync();

        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task ARegionThatFails_DoesNotStopTheOthers_AndRetriesAMinuteLater()
    {
        var broken = Spawn("broken");
        broken.Areas = [];
        await StartAsync(new ScriptedRandom(0), broken, Spawn("forest", minMinutes: 30, maxMinutes: 30));

        await TickAsync();
        Assert.Single(_npcs.Spawns);

        broken.Areas = [new() { X1 = 10, Y1 = 10, X2 = 10, Y2 = 10 }];
        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();
        Assert.Equal(2, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task ABrokenNotice_DoesNotFaultTheSpawns()
    {
        await AddPlayerAsync(1, AccountType.GameMaster);
        // The notice needs {3}, which is never given.
        await StartAsync(new ScriptedRandom(0), [(CommandMessages.SpawnedInOneRegion, "{3}")], Spawn("forest"));

        await TickAsync();

        Assert.True(_service.Running.IsCompletedSuccessfully);
        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task StopAsync_UnregistersTheTimer()
    {
        await StartAsync(new ScriptedRandom(0), Spawn("forest"));
        var timer = TimerId();

        await _service.StopAsync();

        Assert.Equal([timer], _timers.Unregistered);
    }

    public async Task DisposeAsync()
    {
        await _service.StopAsync();
        await _fixture.DisposeAsync();
    }

    private static SpawnTemplate Spawn(
        string id,
        string? name = null,
        int call = 1,
        int max = 1,
        int minMinutes = 0,
        int maxMinutes = 0,
        int x1 = 10,
        int y1 = 10,
        int x2 = 12,
        int y2 = 12
    )
    {
        return new()
        {
            Id = id,
            Map = MapType.Felucca,
            Name = name,
            MobileIds = ["rabbit"],
            Call = call,
            Max = max,
            MinMinutes = minMinutes,
            MaxMinutes = maxMinutes,
            Areas = [new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 }]
        };
    }

    private Task StartAsync(System.Random random, params SpawnTemplate[] spawns)
    {
        return StartAsync(random, [], spawns);
    }

    private async Task StartAsync(System.Random random, (int Id, string Text)[] messages, params SpawnTemplate[] spawns)
    {
        var data = new StubDataLoaderService()
                   .With(spawns)
                   .With(new NpcListTemplate { Id = "unused" })
                   .With(
                       new MobileTemplate { Id = "rabbit", Body = 205 },
                       new MobileTemplate { Id = "dolphin", Body = 151, Movement = MobileMovementType.Water },
                       new MobileTemplate { Id = "walrus", Body = 221, Movement = MobileMovementType.Both }
                   );
        (int Id, string Text)[] defaults =
        [
            (CommandMessages.SpawnedInOneRegion, "Spawn: {0} ({1}): {2} NPCs"),
            (CommandMessages.SpawnedInRegions, "Spawn: {0} NPCs in {1} regions: {2}"),
            (CommandMessages.SpawnedWorldProgress, "{0} - world {1}/{2} ({3}%)"),
            (CommandMessages.SpawnedAndMore, "{0} and {1} more")
        ];
        var localization = TestLocalization.With(
            messages.Concat(defaults.Where(message => messages.All(given => given.Id != message.Id))).ToArray()
        );
        _service = new(
            data,
            _map,
            _movement,
            _npcs,
            _fixture.Mobiles,
            _fixture.Sessions,
            _fixture.Sender,
            localization,
            _timers,
            _fixture.Network.Loop,
            _clock,
            random,
            new SpawnsConfig { InitialFill = _initialFill }
        );
        await _service.StartAsync();
    }

    // The timer fires on the game loop; the spawns it starts are awaited too.
    private async Task TickAsync()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _timers.Fire(TimerId()));
        await _service.Running;
    }

    private string TimerId()
    {
        return _timers.Timers.Single(timer => timer.Name == SpawnRegionService.TimerName).Id;
    }

    private async Task<MobileEntity> AddLiveAsync(string region)
    {
        var npc = new MobileEntity { Id = new Serial(_nextSerial++), Name = "Rabbit", TemplateId = "rabbit", Map = MapType.Felucca };
        npc.SetProp(SpawnRegionService.RegionProp, region);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(npc));

        return npc;
    }

    private async Task<long> AddPlayerAsync(long id, AccountType type)
    {
        var session = await _fixture.AddAsync(id, map: MapType.Felucca);
        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountType, type));

        return id;
    }

    private List<(long Session, string Text)> Notices()
    {
        return _fixture.Sender.Sent
                       .Select((packet, index) => (packet, index))
                       .Where(pair => pair.packet is UnicodeSpeechMessagePacket)
                       .Select(pair => (_fixture.Sender.SentSessionIds[pair.index], ((UnicodeSpeechMessagePacket)pair.packet).Text))
                       .ToList();
    }
}
