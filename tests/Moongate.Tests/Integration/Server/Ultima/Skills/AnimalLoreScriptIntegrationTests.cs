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
using Moongate.Scripting.Modules;
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
using Moongate.Core.Types.Geometry;
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
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Data.Bodies;
namespace Moongate.Tests.Integration.Server.Ultima.Skills;

/// <summary>
///     The shipped <c>scripts/skills/animal_lore.lua</c> with the real Lua engine, skill check and pet module: a player
///     at the origin of the fixture and a horse that asks 29.1 two tiles east of it.
/// </summary>
public sealed class AnimalLoreScriptIntegrationTests : IAsyncLifetime
{
    private const int Which = 500328;
    private const int NotAnAnimal = 500329;
    private const int TooFar = 500446;
    private const int NoSight = 1049654;
    private const int Failed = 500334;
    private const int OnlyTamed = 1049674;
    private const int OnlyTameable = 1049675;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingGumpService _gumps = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _time = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubTargetService _targets = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;

    private readonly TamingService _taming = new(
        new StubDataLoaderService().With(
            new TamingCreature { Template = "horse", MinSkill = 29.1, Slots = 1, Food = ["fruit", "grain"] },
            new TamingCreature { Template = "lizard", MinSkill = 80, Slots = 1, Food = [] }
        )
    );

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "horse", Damage = DiceSpec.Parse("1d6+2"), ScriptId = "animal" },
            new MobileTemplate { Id = "orc", ScriptId = "monster" },
            new MobileTemplate { Id = "lizard", ScriptId = "animal" }
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

    public AnimalLoreScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _horse = Creature(0x100, "horse", 0xE2, 2);
        _horse.Armor = 6;
        _horse.Strength = 90;
        _horse.Dexterity = 80;
        _horse.Intelligence = 10;
        _horse.HitsMax = 50;
        _horse.Hits = 40;
        _horse.StaminaMax = 60;
        _horse.Stamina = 60;

        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.AnimalLore, GainFactor = 1.0, Delay = 1 }
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
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<ITamingService>(_taming);
        _container.RegisterInstance<IPetService>(_pets);
        _container.RegisterInstance<IMobileTemplateService>(_templates);
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
        _container.RegisterInstance<IGumpService>(_gumps);
        var root = Path.GetFullPath(Path.Combine(ShippedScript("skills/animal_lore.lua"), "..", "..", ".."));
        var gumpTemplates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        GumpScriptService? gumpScripts = null;
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance<IDataLoaderService>(
            new StubDataLoaderService().With(
                new BodyContent { Body = new(0xE2), Type = BodyType.Animal },
                new BodyContent { Body = new(0x11), Type = BodyType.Monster },
                new BodyContent { Body = new(0x190), Type = BodyType.Human }
            )
        );
        _container.RegisterInstance<ISkillService>(new SkillService(_state, data, new SkillsConfig(), _random));
        _container.RegisterScriptEnum<BodyType>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<PetModule>();
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<LogModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
        _scripts.Write("skills/animal_lore.lua", File.ReadAllText(ShippedScript("skills/animal_lore.lua")));
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
        _skillScripts = new SkillScriptService(_engine, _loop, options);
        await _skillScripts.StartAsync();
        _use = new(_fixture.Mobiles, _skillScripts, _speech, _time, data);
        _loop.DeferTryPost = true;
        Skill(120);
        _random.Rest = 0.0;
        _targets.Result = TargetResult.ForObject(_horse.Id);
    }

    [Fact]
    public void ATamedHorse_IsLookedAt_AndOpensAGumpOfTwoPages()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _horse.SetProp(MountProps.PetLoyalty, 75);

        Use();

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal([Which], Told());
        var gump = Assert.Single(_gumps.Opened).Gump;
        Assert.Equal("animal_lore", gump.Id);
        var cliloc = gump.Layout.Entries.OfType<GumpHtmlLocalized>().Select(entry => entry.Cliloc).ToList();
        // The seventh word of the loyalty ladder, 1049595 + 75 / 10.
        Assert.Contains(1049595 + 7, cliloc);
        Assert.DoesNotContain(1061643, cliloc);
        // The food it eats: fruit and vegetables, grains and hay.
        Assert.Contains(1049565, cliloc);
        Assert.Contains(1049566, cliloc);
        Assert.DoesNotContain(1049564, cliloc);
        var text = gump.Layout.Entries.OfType<GumpText>().Select(entry => entry.Text).ToList();
        Assert.Contains("a horse", text);
        Assert.Contains("40/50", text);
        Assert.Contains("3-8", text);
        Assert.Contains("6", text);
        Assert.Equal(1, text.Count(entry => entry == "6"));
        Assert.Contains("29.1", text);
        // Two pages: a button to each.
        Assert.Equal([2, 1], gump.Layout.Entries.OfType<GumpButton>().Select(button => button.Page));
    }

    [Theory, InlineData(true, 1049608), InlineData(false, 502006)]
    public void ATamedHorse_ShowsWhetherItIsBondedOrJustTame(bool bonded, int cliloc)
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);

        if (bonded)
        {
            _horse.SetProp(MountProps.PetBonded, true);
        }

        Use();

        var shown = Assert.Single(_gumps.Opened).Gump.Layout.Entries.OfType<GumpHtmlLocalized>().Select(entry => entry.Cliloc).ToList();
        Assert.Contains(cliloc, shown);
        Assert.DoesNotContain(bonded ? 502006 : 1049608, shown);
    }

    [Fact]
    public void AWildHorse_ForSomeoneWithLoreOneHundred_IsWild()
    {
        Skill(100);
        _random.Rest = 0.0;

        Use();

        var cliloc = Assert.Single(_gumps.Opened).Gump.Layout.Entries.OfType<GumpHtmlLocalized>().Select(entry => entry.Cliloc).ToList();
        Assert.Contains(1061643, cliloc);
    }

    [Fact]
    public void ACreatureThatEatsNothing_ShowsNone()
    {
        var lizard = Creature(0x101, "lizard", 0xE2, 3);
        lizard.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _targets.Result = TargetResult.ForObject(lizard.Id);

        Use();

        var cliloc = Assert.Single(_gumps.Opened).Gump.Layout.Entries.OfType<GumpHtmlLocalized>().Select(entry => entry.Cliloc).ToList();
        Assert.Contains(3000340, cliloc);
    }

    [Theory,
     InlineData(99.9, "wild", OnlyTamed),
     InlineData(109.9, "orc", OnlyTameable)]
    public void ALoreTooLowForWhatIsPicked_IsRefused(double points, string what, int cliloc)
    {
        Skill(points);
        var target = what == "orc" ? Creature(0x102, "orc", 0x11, 2) : _horse;

        if (what == "orc")
        {
            _targets.Result = TargetResult.ForObject(target.Id);
        }

        Use();

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal([Which, cliloc], Told());
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void ATameableCreature_FromOneHundred_IsLookedAt()
    {
        Skill(100);
        _random.Rest = 0.0;

        Use();

        Assert.Single(_gumps.Opened);
    }

    [Fact]
    public void AnUntameableCreature_FromOneHundredTen_IsLookedAt()
    {
        Skill(110);
        _random.Rest = 0.0;
        var orc = Creature(0x102, "orc", 0x11, 2);
        _targets.Result = TargetResult.ForObject(orc.Id);

        Use();

        Assert.Single(_gumps.Opened);
    }

    [Theory,
     InlineData("player", NotAnAnimal),
     InlineData("human", NotAnAnimal)]
    public void WhatIsNoAnimal_OrIsDead_IsRefusedWithTheClientsText(string what, int cliloc)
    {
        var target = what switch
        {
            "player" => _aria,
            "human" => Creature(0x103, "orc", 0x190, 2),
            _ => _horse
        };

        _targets.Result = TargetResult.ForObject(target.Id);
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);

        Use();

        Assert.Equal([Which, cliloc], Told());
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void AnAnimalFarAway_IsTooFar()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _horse.Location = new Point3D(_aria.Location.X + 9, _aria.Location.Y, _aria.Location.Z);

        Use();

        Assert.Equal([Which, TooFar], Told());
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void ADiagonalFarAway_IsTooFar_AsTheDistanceIsTheLargerOfTheTwo()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _horse.Location = new Point3D(_aria.Location.X + 2, _aria.Location.Y + 9, _aria.Location.Z);

        Use();

        Assert.Equal([Which, TooFar], Told());
    }

    [Fact]
    public void AHorseOnAnotherMap_IsTooFar()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _horse.Map = MapType.Ilshenar;

        Use();

        Assert.Equal([Which, TooFar], Told());
    }

    [Fact]
    public void AHorseBehindAWall_CannotBeSeen()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _sight.Allow = false;

        Use();

        Assert.Equal([Which, NoSight], Told());
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void TheReachIsCheckedBeforeTheSkill_AFarWildHorseIsTooFar_NotOnlyTamed()
    {
        Skill(50);
        _horse.Location = new Point3D(_aria.Location.X + 20, _aria.Location.Y, _aria.Location.Z);

        Use();

        Assert.Equal([Which, TooFar], Told());
    }

    [Fact]
    public void AStatOfZero_ShowsNone_NotZero()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        _horse.Armor = 0;
        _horse.Intelligence = 0;

        Use();

        var text = Assert.Single(_gumps.Opened).Gump.Layout.Entries.OfType<GumpText>().Select(entry => entry.Text).ToList();
        Assert.DoesNotContain("0", text);
        Assert.Contains("---", text);
    }

    [Fact]
    public void ALabelOfTheGump_IsDrawnInTheLabelColor()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);

        Use();

        var html = Assert.Single(_gumps.Opened).Gump.Layout.Entries.OfType<GumpHtmlLocalized>().ToList();
        Assert.All(html, entry => Assert.NotNull(entry.Color));
    }

    [Fact]
    public void ARollThatFails_SaysYouCannotThinkOfAnything()
    {
        _horse.SetProp(MountProps.Owner, (long)_aria.Id.Value);
        Skill(1);
        _random.Rest = 0.999;

        Use();

        Assert.Equal([Which, Failed], Told());
        Assert.Empty(_gumps.Opened);
    }

    // --- helpers ---

    private MobileEntity Creature(uint serial, string template, int body, int tilesEast)
    {
        var creature = new MobileEntity
        {
            Id = new(serial), Name = template == "horse" ? "a horse" : template, TemplateId = template, Body = body,
            Map = _aria.Map, Direction = DirectionType.North, Hits = 20, HitsMax = 20,
            Location = new Point3D(_aria.Location.X + tilesEast, _aria.Location.Y, _aria.Location.Z)
        };
        _fixture.Mobiles.EnterWorld(creature);

        return creature;
    }

    private void Skill(double points)
    {
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.AnimalLore, Base = (int)(points * 10) });
        _aria.Skills = [new MobileSkill { Skill = SkillType.AnimalLore, Base = (int)(points * 10) }];
    }

    private void Use()
    {
        _use.Use(_session, SkillType.AnimalLore);
        _loop.RunDeferred();
        _time.Advance(TimeSpan.FromSeconds(2));
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
