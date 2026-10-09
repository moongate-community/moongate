using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Pets;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Data.Gumps;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

/// <summary>
///     The shipped <c>common/pet_orders.lua</c> in <c>common/creature.lua</c> and the <c>animal</c> script, on the real Lua engine
///     and the real modules: a horse that belongs to a player five tiles east of it, and the words the player says to it.
/// </summary>
public sealed class PetOrdersScriptIntegrationTests : IAsyncLifetime
{
    private const long Owner = 2;
    private const int StepsToTheOwner = 3;

    private readonly RecordingCombatService _combat = new();
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubPathfindingService _finder = new();
    private readonly StubTargetService _targets = new();
    private readonly StubPetService _pets = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "horse", ScriptId = "animal" },
            new MobileTemplate { Id = "orc", ScriptId = "monster" }
        )
    );

    private readonly MobileEntity _horse = new()
    {
        Id = new Serial(0x100), Name = "a horse", TemplateId = "horse", Body = 0xE2, Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North, Hits = 20, HitsMax = 20
    };

    private readonly MobileEntity _llama = new()
    {
        Id = new Serial(0x101), Name = "a llama", TemplateId = "horse", Body = 0xDC, Map = MapType.Trammel,
        Location = new Point3D(1601, 1601, 0), Direction = DirectionType.North, Hits = 20, HitsMax = 20
    };

    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x200), Name = "an orc", TemplateId = "orc", Body = 0x11, Map = MapType.Trammel,
        Location = new Point3D(1606, 1601, 0), Hits = 20, HitsMax = 20
    };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private LuaScriptEngineService _engine = null!;
    private NpcScriptService _npcs = null!;
    private NpcHearingService _hearing = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1605, 1600, 0)));
        _horse.SetProp("owner", Owner);
        _llama.SetProp("owner", Owner);
        _fixture.Mobiles.EnterWorld(_horse);
        _fixture.Mobiles.EnterWorld(_orc);
        var time = new SettableClock();
        GumpScriptService? gumpScripts = null;
        var root = Path.GetFullPath(Path.Combine(ShippedScript("common/creature.lua"), "..", "..", ".."));
        var gumpTemplates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ITeleportService>(_teleports);
        _container.RegisterInstance<IMobileTemplateService>(_templates);
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IPathfindingService>(_finder);
        _container.RegisterInstance<INpcPathService>(new NpcPathService(_finder, time));
        _container.RegisterInstance<IMovementService>(new StubMovementService());
        _container.RegisterInstance<IItemService>(TestItems.Create(_fixture.Sectors));
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<TimeProvider>(time);
        _container.RegisterInstance<ICombatService>(_combat);
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<IPetService>(_pets);
        _container.RegisterInstance<ITamingService>(
            new TamingService(new StubDataLoaderService().With(new TamingCreature { Template = "horse", MinSkill = 29.1 }))
        );
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<DiceModule>();
        _container.AddScriptModule<CombatModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<PetModule>();
        _container.AddScriptModule<GumpModule>();
        _container.RegisterScriptEnum<MonsterAnimationType>();
        _container.RegisterScriptEnum<BodyType>();
        _container.RegisterScriptEnum<SpeechKeywordType>();
        _container.RegisterInstance<IDataLoaderService>(
            new StubDataLoaderService().With(
                new BodyContent { Body = new(0xE2), Type = BodyType.Animal },
                new BodyContent { Body = new(0xDC), Type = BodyType.Animal },
                new BodyContent { Body = new(0x11), Type = BodyType.Monster },
                new BodyContent { Body = new(0x190), Type = BodyType.Human }
            )
        );
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        foreach (var script in new[]
                     { "common/creature.lua", "common/pet_orders.lua", "mobiles/animal.lua", "mobiles/monster.lua", "gumps/pet_release.lua" })
        {
            _scripts.Write(script, File.ReadAllText(ShippedScript(script)));
        }

        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _npcs = new(_engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
        await _npcs.StartAsync();
        _hearing = new NpcHearingService(_npcs, _fixture.Sectors);
        _loop.DeferTryPost = true;
    }

    [Fact]
    public void ATamedHorse_FollowsItsOwner_UntilItIsTwoTilesFromIt()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);

        Think(StepsToTheOwner);
        Assert.Equal(new Point3D(1603, 1600, 0), _horse.Location);

        Think(5);
        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(new Point3D(1603, 1600, 0), _horse.Location);
    }

    [Fact]
    public void AHorseThatFollowsTheOwner_DoesNotStrollOnItsOwn()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1601, 1600, 0)));

        Think(40);

        Assert.Equal(new Point3D(1600, 1600, 0), _horse.Location);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AHorseThatCannotGetThere_IsMovedBesideItsOwnerAfterTenSteps()
    {
        Think(9);
        Assert.Empty(_teleports.Teleports);

        Think(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(
            (_horse, MapType.Trammel, new Point3D(1605, 1600, 0)),
            Assert.Single(_teleports.Teleports)
        );
    }

    [Theory, InlineData(30), InlineData(25)]
    public void AHorseFarFromItsOwner_StaysWhereItIs(int tilesAway)
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1600 + tilesAway, 1600, 0)));
        _finder.Finds(DirectionType.East);

        Think(5);

        Assert.Equal(new Point3D(1600, 1600, 0), _horse.Location);
        Assert.Empty(_finder.Searches);
    }

    [Fact]
    public void AHorseWhoseOwnerIsNotInTheWorld_StaysWhereItIs()
    {
        _fixture.Mobiles.LeaveWorld(_aria.Id);
        _finder.Finds(DirectionType.East);

        Think(5);

        Assert.Equal(new Point3D(1600, 1600, 0), _horse.Location);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AnOrderToStay_KeepsTheHorseWhereItIs()
    {
        _horse.SetProp("pet.order", "stay");
        _finder.Finds(DirectionType.East, DirectionType.East);

        Think(6);

        Assert.Equal(new Point3D(1600, 1600, 0), _horse.Location);
    }

    [Fact]
    public void AnOrderToCome_WalksToTheOwner_AndThenStays()
    {
        _horse.SetProp("pet.order", "come");
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);

        Think(StepsToTheOwner + 1);

        Assert.Equal(new Point3D(1603, 1600, 0), _horse.Location);
        Assert.Equal("stay", _horse.GetProp<string>("pet.order"));
    }

    [Fact]
    public void AGuard_FightsWhoFightsItsOwner_OrItself_ButNotAnotherPetOfTheOwner()
    {
        _horse.SetProp("pet.order", "guard");
        _fixture.Mobiles.EnterWorld(_llama);
        _sight.Allow = true;
        _combat.Attacks.Add((_llama, _orc));
        _combat.Attacks.Add((_orc, _aria));

        Think(4);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Contains((_horse, _orc), _combat.Attacks);
        Assert.DoesNotContain(_combat.Attacks, attack => attack.Attacker == _horse && attack.Target == _llama);
    }

    [Fact]
    public void AGuard_WithNobodyToFight_StaysNearItsOwner()
    {
        _horse.SetProp("pet.order", "guard");
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);

        Think(StepsToTheOwner);

        Assert.Equal(new Point3D(1602, 1600, 0), _horse.Location);
        Assert.DoesNotContain(_combat.Attacks, attack => attack.Attacker == _horse);
    }

    [Theory,
     InlineData(SpeechKeywordType.AllStay, "stay"),
     InlineData(SpeechKeywordType.AllStop, "stay"),
     InlineData(SpeechKeywordType.AllCome, "come"),
     InlineData(SpeechKeywordType.AllFollow, "follow"),
     InlineData(SpeechKeywordType.AllFollowMe, "follow"),
     InlineData(SpeechKeywordType.AllGuard, "guard"),
     InlineData(SpeechKeywordType.AllGuardMe, "guard")]
    public void TheAllWords_OfTheOwner_GiveTheOrderToEveryPetNear(SpeechKeywordType word, string order)
    {
        _fixture.Mobiles.EnterWorld(_llama);

        Say("all words", word);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal([order, order], new[] { _horse, _llama }.Select(pet => pet.GetProp("pet.order", "follow")));
    }

    [Fact]
    public void TheWordStop_StopsTheFight_AndMakesThePetStay()
    {
        _combat.Attacks.Add((_horse, _orc));

        Say("all stop", SpeechKeywordType.AllStop);

        Assert.Contains(_horse, _combat.Stopped);
        Assert.Equal("stay", _horse.GetProp<string>("pet.order"));
    }

    [Theory,
     InlineData(SpeechKeywordType.PetStay, "stay"),
     InlineData(SpeechKeywordType.PetCome, "come"),
     InlineData(SpeechKeywordType.PetGuard, "guard"),
     InlineData(SpeechKeywordType.PetFollow, "follow"),
     InlineData(SpeechKeywordType.PetFollowMe, "follow")]
    public void TheNamedWords_GiveTheOrderToThePetNamed_AndToNoOtherPet(SpeechKeywordType word, string order)
    {
        _fixture.Mobiles.EnterWorld(_llama);
        _horse.SetProp("pet.order", "stay");
        _llama.SetProp("pet.order", "stay");

        Say("a horse something", word);

        Assert.Equal(order, _horse.GetProp<string>("pet.order"));
        Assert.Equal("stay", _llama.GetProp<string>("pet.order"));
    }

    [Fact]
    public void ANamedWord_WithoutTheNameFirst_IsNotForThePet()
    {
        Say("stay there", SpeechKeywordType.PetStay);

        Assert.False(_horse.TryGetProp<string>("pet.order", out _));
    }

    [Fact]
    public void TheWordsOfSomeoneElse_OfAGhost_OrFromFarAway_AreNotObeyed()
    {
        var other = new MobileEntity
        {
            Id = new Serial(3), Name = "Bruno", AccountId = new Serial(0x43), Map = MapType.Trammel,
            Location = new Point3D(1601, 1600, 0)
        };
        _fixture.Mobiles.EnterWorld(other);

        _hearing.Heard(other, "all stay", [(int)SpeechKeywordType.AllStay]);
        _aria.Body = 0x0192;
        Say("all stay", SpeechKeywordType.AllStay);
        _aria.Body = 0x0190;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1615, 1600, 0)));
        Say("all stay", SpeechKeywordType.AllStay);

        Assert.False(_horse.TryGetProp<string>("pet.order", out _));
    }

    [Fact]
    public void ACreatureThatBelongsToNobody_DoesNotObey()
    {
        _horse.RemoveProp("owner");

        Say("all stay", SpeechKeywordType.AllStay);

        Assert.False(_horse.TryGetProp<string>("pet.order", out _));
    }

    [Fact]
    public void AllKill_AsksForATarget_AndEveryPetFightsIt()
    {
        _fixture.Mobiles.EnterWorld(_llama);
        _targets.Result = TargetResult.ForObject(_orc.Id);

        Say("all kill", SpeechKeywordType.AllKill);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Contains((_horse, _orc), _combat.Attacks);
        Assert.Contains((_llama, _orc), _combat.Attacks);
    }

    [Theory, InlineData("owner"), InlineData("pet"), InlineData("cancel")]
    public void AllKill_OfTheOwnerOfAPetOrOfNothing_FightsNobody(string pick)
    {
        _targets.Result = pick switch
        {
            "owner" => TargetResult.ForObject(_aria.Id),
            "pet" => TargetResult.ForObject(_horse.Id),
            _ => TargetResult.Canceled(TargetCancelType.Canceled)
        };

        Say("all kill", SpeechKeywordType.AllKill);

        Assert.Empty(_combat.Attacks);
    }

    [Fact]
    public void ANamedRelease_AsksFirst_AndYesLetsThePetGo()
    {
        Say("a horse release", SpeechKeywordType.PetRelease);

        var gump = Assert.Single(_gumps.Opened);
        Assert.Equal("pet_release", gump.Gump.Id);
        Assert.Empty(_pets.Released);

        Answer(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal((_aria, _horse), Assert.Single(_pets.Released));
    }

    [Fact]
    public void ANamedRelease_AnsweredNo_KeepsThePet()
    {
        Say("a horse release", SpeechKeywordType.PetRelease);

        Answer(2);

        Assert.Empty(_pets.Released);
    }

    [Fact]
    public void ARelease_OfAPetThatWentFarOrWasTamedByAnother_DoesNothing()
    {
        Say("a horse release", SpeechKeywordType.PetRelease);
        _horse.SetProp("owner", 77L);

        Answer(1);

        Assert.Empty(_pets.Released);
    }

    [Fact]
    public void ATamedWolfLikeMonster_StillFollowsItsOwner_AndDoesNotHuntIt()
    {
        var wolf = new MobileEntity
        {
            Id = new Serial(0x300), Name = "a wolf", TemplateId = "orc", Body = 0x11, Map = MapType.Trammel,
            Location = new Point3D(1600, 1601, 0), Hits = 20, HitsMax = 20
        };
        wolf.SetProp("owner", Owner);
        _fixture.Mobiles.EnterWorld(wolf);
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);

        _npcs.Think(wolf);
        _npcs.Think(wolf);

        Assert.DoesNotContain(_combat.Attacks, attack => attack.Attacker == wolf);
        Assert.Equal(new Point3D(1602, 1601, 0), wolf.Location);
    }

    // --- helpers ---

    private void Say(string text, SpeechKeywordType word)
    {
        _hearing.Heard(_aria, text, [(int)word]);
        _loop.RunDeferred();
    }

    private void Answer(int button)
    {
        _gumps.Opened[0]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
        _loop.RunDeferred();
    }

    private void Think(int times)
    {
        for (var think = 0; think < times; think++)
        {
            _npcs.Think(_horse);
        }
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
