using Moongate.Tests.TestSupport.Ultima.Pets;
using Moongate.Server.Ultima.Types.Pets;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Tests.TestSupport.Ultima.Mounts;
using DryIoc;
using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.TestSupport.Ultima.Combat;
namespace Moongate.Tests.Integration.Server.Ultima.Skills;

/// <summary>
///     The shipped <c>scripts/skills/animal_taming.lua</c>, with the real Lua engine, skill check, pet service and taming
///     data: a player at 1600,1600 and a horse that asks 29.1 two tiles east of it.
/// </summary>
public sealed class AnimalTamingScriptIntegrationTests : IAsyncLifetime
{
    private const int Which = 502789;
    private const int NotACreature = 502801;
    private const int NotAnAnimal = 502469;
    private const int NotTamable = 1049655;
    private const int AlreadyTamed = 502804;
    private const int TooMany = 1049611;
    private const int NoChance = 502806;
    private const int TooFar = 500446;
    private const int Busy = 502802;
    private const int Start = 1010597;
    private const int Wandered = 502795;
    private const int Dead = 502796;
    private const int NoSight = 1049654;
    private const int Hurt = 502794;
    private const int Accept = 502799;
    private const int Failed = 502798;

    // What the script's coin gives: under one half the taming takes three times, over it four.
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubCombatGearService _gear = new();
    private readonly RecordingCombatService _combat = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _time = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubTargetService _targets = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;

    private readonly TamingService _taming = new(
        new StubDataLoaderService().With(
            new TamingCreature { Template = "horse", MinSkill = 29.1, Slots = 1 },
            new TamingCreature { Template = "drake", MinSkill = 90, Slots = 3 },
            new TamingCreature { Template = "cat", MinSkill = -0.9, Slots = 1 }
        )
    );

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private SkillScriptService _skillScripts = null!;
    private SkillUseService _use = null!;
    private PetService _pets = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _horse = null!;

    public AnimalTamingScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _horse = Creature(0x100, "horse", 2);

        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.AnimalTaming, GainFactor = 1.0, Delay = 30 }
        );
        _pets = new PetService(_fixture.Mobiles, _items, _taming, new PetsConfig(), _time);
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ICombatGearService>(_gear);
        _container.RegisterInstance<ICombatService>(_combat);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<ITamingService>(_taming);
        _container.RegisterInstance<IPetService>(_pets);
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(
                _fixture.Mobiles,
                _view,
                _fixture.Sessions,
                _fixture.Sender,
                _fixture.Sectors,
                new StubBankService()
            )
        );
        _container.RegisterInstance<ISkillService>(new SkillService(_state, data, new SkillsConfig(), _random));
        _container.RegisterScriptEnum<BodyType>();
        _container.RegisterScriptEnum<MapType>();
        _container.RegisterScriptEnum<PetResultType>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<PetModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
        // The coin of the script comes up the same each time: low, so a tame takes three times.
        _scripts.Write(
            "skills/animal_taming.lua",
            File.ReadAllText(ShippedScript("skills/animal_taming.lua"))
                .Replace("animal_taming.roll = math.random", "animal_taming.roll = function() return 0.1 end")
        );
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
        _skillScripts = new SkillScriptService(_engine, _loop, options);
        await _skillScripts.StartAsync();
        _use = new(_fixture.Mobiles, _skillScripts, _speech, _time, data);
        // A coin that comes up the same each time, and the cursor answers on the next turn of the loop.
        _loop.DeferTryPost = true;
        Skill(100);
        PickHorse();
    }

    [Fact]
    public void ASkilledTamer_WhoStaysNearAHorse_TamesIt_AfterThreeTimes()
    {
        Use();

        Assert.Empty(_errors);
        Assert.Equal([Which, Start], Told());
        Assert.Single(_timers.Timers);

        Fire();
        Fire();
        Assert.Empty(_errors);
        Assert.Equal(2, Told().Count(cliloc => cliloc is >= 502790 and <= 502793 or >= 1005608 and <= 1005613 or >= 1010593 and <= 1010596));
        Fire();

        Assert.Empty(_errors);
        Assert.Equal(Accept, Told()[^1]);
        Assert.Equal((long)_aria.Id.Value, _horse.GetProp<long>(MountProps.Owner));
        Assert.Equal(1, _pets.Followers(_aria));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void ARollThatFails_SaysSo_AndTheHorseStaysWild()
    {
        // The window is 29.0 to 79.0: a skill of 29.0 is the chance of nothing, rolled low.
        Skill(29.5);
        _random.Rest = 0.999;

        TameUntilTheRoll();

        Assert.Equal(Failed, Told()[^1]);
        Assert.False(_horse.TryGetProp<long>(MountProps.Owner, out _));
    }

    [Fact]
    public void ASkillUnderTheMinimum_HasNoChance()
    {
        Skill(29.0);

        Use();

        Assert.Equal([Which, NoChance], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void ASmallAnimalThatAsksLessThanNone_CanBeTamedByWhoHasNoSkill()
    {
        Skill(0);
        var cat = Creature(0x101, "cat", 1);
        _targets.Result = TargetResult.ForObject(cat.Id);

        Use();

        Assert.Equal([Which, Start], Told());
    }

    [Theory,
     InlineData("item", NotACreature),
     InlineData("gone", NotACreature),
     InlineData("player", NotAnAnimal),
     InlineData("orc", NotTamable),
     InlineData("owned", AlreadyTamed)]
    public void WhatCannotBeTamed_IsRefusedWithTheClientsText(string what, int cliloc)
    {
        var target = what switch
        {
            "item" => new Serial(0x40000999),
            "gone" => new Serial(0x999),
            "player" => _aria.Id,
            "orc" => Creature(0x102, "orc", 2).Id,
            _ => _horse.Id
        };

        if (what == "owned")
        {
            _horse.SetProp(MountProps.Owner, 77L);
        }

        _targets.Result = TargetResult.ForObject(target);

        Use();

        Assert.Empty(_errors);
        Assert.Equal([Which, cliloc], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void AHorseFarFromThePick_IsTooFar()
    {
        _horse.Location = new Point3D(_aria.Location.X + 4, _aria.Location.Y, _aria.Location.Z);

        Use();

        Assert.Equal([Which, TooFar], Told());
    }

    [Fact]
    public void WhenTheFollowersDoNotFit_TheCreatureIsRefused()
    {
        for (var index = 0; index < 5; index++)
        {
            var own = Creature((uint)(0x200 + index), "horse", 3);
            own.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        }

        Use();

        Assert.Equal([Which, TooMany], Told());
    }

    [Fact]
    public void UsingTheSkillAgainWhileTaming_DoesNothingButWait()
    {
        Use();
        _speech.ToldClilocs.Clear();

        // A second use by the same tamer while it tames does nothing but wait.
        Use();

        Assert.Empty(_speech.ToldClilocs);
        Assert.Single(_timers.Timers);
    }

    [Theory,
     InlineData("far", Wandered),
     InlineData("dead", Dead),
     InlineData("blind", NoSight),
     InlineData("taken", AlreadyTamed),
     InlineData("hurt", Hurt)]
    public void WhenSomethingChangesBetweenTheTimes_TheTamingStops_AndTheCreatureStaysWild(string change, int cliloc)
    {
        Use();
        Fire();

        switch (change)
        {
            case "far":
                _horse.Location = new Point3D(_aria.Location.X + 8, _aria.Location.Y, _aria.Location.Z);

                break;
            case "dead":
                _aria.Body = 0x0192;

                break;
            case "blind":
                _sight.Allow = false;

                break;
            case "taken":
                _horse.SetProp(MountProps.Owner, 77L);

                break;
            default:
                _horse.Hits -= 3;

                break;
        }

        Fire();

        Assert.Empty(_errors);
        Assert.Equal(cliloc, Told()[^1]);
        Assert.Empty(_timers.Timers);
        Assert.NotEqual((long)_aria.Id.Value, _horse.GetProp(MountProps.Owner, 0L));
    }

    [Fact]
    public void AfterAnInterruption_TheTamerMayTryAgain()
    {
        Use();
        _sight.Allow = false;
        Fire();
        _sight.Allow = true;
        _speech.ToldClilocs.Clear();

        Use();

        Assert.Equal([Which, Start], Told());
    }

    // --- helpers ---

    private MobileEntity Creature(uint serial, string template, int tilesEast)
    {
        var creature = new MobileEntity
        {
            Id = new Serial(serial), Name = template, TemplateId = template, Map = _aria.Map,
            Location = new Point3D(_aria.Location.X + tilesEast, _aria.Location.Y, _aria.Location.Z), Hits = 20, HitsMax = 20
        };
        _fixture.Mobiles.EnterWorld(creature);

        return creature;
    }

    private void Skill(double points)
    {
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.AnimalTaming, Base = (int)(points * 10) });
        _aria.Skills = [new MobileSkill { Skill = SkillType.AnimalTaming, Base = (int)(points * 10) }];
    }

    private void PickHorse()
    {
        _targets.Result = TargetResult.ForObject(_horse.Id);
    }

    private void Use()
    {
        _use.Use(_session, SkillType.AnimalTaming);
        _loop.RunDeferred();
        _time.Advance(TimeSpan.FromSeconds(2));
    }

    private void Fire()
    {
        _timers.Fire(_timers.Timers.First().Id);
        _loop.RunDeferred();
    }

    private void TameUntilTheRoll()
    {
        Use();

        while (_timers.Timers.Count > 0)
        {
            Fire();
        }
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player == _aria).Select(told => told.Cliloc).ToList();
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
