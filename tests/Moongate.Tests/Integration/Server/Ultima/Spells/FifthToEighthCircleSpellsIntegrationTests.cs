using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Ultima.ContextMenus;
using Moongate.Tests.TestSupport.Ultima.Containers;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Primitives;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Types.Pets;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Pets;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Spells;

/// <summary>
///     The shipped fifth to eighth circles: the scripts of <c>scripts/spells</c>, the field, summon and gate scripts, the
///     gumps of Resurrection and Polymorph and <c>scripts/common</c> over the real cast, pet, paralysis and disguise
///     services and the shipped spells and item templates, from the cast to the effect.
/// </summary>
public sealed class FifthToEighthCircleSpellsIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Bran = 3;
    private const int HumanBody = 400;
    private const int OrcBody = 17;
    private const int WontWork = 501857;

    private static readonly string[] Keys =
    [
        "blade_spirits", "dispel_field", "incognito", "magic_reflection", "mind_blast", "paralyze", "poison_field",
        "summon_creature", "dispel", "energy_bolt", "explosion", "invisibility", "mark", "mass_curse", "paralyze_field",
        "reveal", "chain_lightning", "energy_field", "flame_strike", "gate_travel", "mana_vampire", "mass_dispel",
        "meteor_swarm", "polymorph", "earthquake", "energy_vortex", "resurrection", "summon_air_elemental",
        "summon_daemon", "curse", "clumsy", "weaken", "feeblemind", "poison", "mana_drain", "summon_earth_elemental", "summon_fire_elemental", "summon_water_elemental"
    ];

    private static readonly string[] Summons =
    [
        "bladespirit_summon", "energyvortex_summon", "airele_summon", "earthele_summon", "firele_summon", "waterele_summon",
        "daemon_summon"
    ];

    private static readonly string[] Animals =
    [
        "polarbear", "brownbear", "blackbear", "horse", "walrus", "chicken", "scorpion", "giantserpent", "llama",
        "alligator", "greywolf", "slime", "eagle", "gorilla", "snowleopard", "pig", "hind", "rabbit"
    ];

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingTargetService _targets = new();
    private readonly StubSkillService _skills = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingEffectService _effects = new();
    private readonly StubMovementService _movement = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly StubContainerCapacityService _capacity = new();
    private readonly StubInventoryMutationGuard _guard = new();
    private readonly SettableClock _time = new();
    private readonly FakeMapService _map = new(64, 64, MapType.Trammel);
    private readonly RecordingCombatService _combat = new();
    private readonly RecordingUseService _uses = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly StubNpcService _npcService = new();
    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private ItemTimerService _itemTimers = null!;
    private NpcScriptService _npcScripts = null!;
    private ItemService _items = null!;
    private ItemTemplateService _templates = null!;
    private SpellCastService _casts = null!;
    private SpellbookService _books = null!;
    private StatBonusService _bonuses = null!;
    private ParalysisService _paralysis = null!;
    private DisguiseService _disguise = null!;
    private PetService _pets = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bran = null!;
    private ItemEntity _book = null!;
    private uint _next = 0x40000050;
    private uint _nextMobile = 0x500;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _items = TestItems.Create(_fixture.Sectors);
        await _fixture.AddAsync((int)Aria);
        await _fixture.AddAsync((int)Bran);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Bran), out _bran!));
        _aria.AccountId = new Serial(0x42);
        _bran.AccountId = new Serial(0x43);
        (_aria.Body, _bran.Body) = (HumanBody, HumanBody);
        (_aria.Name, _bran.Name) = ("Aria", "Bran");
        (_aria.Mana, _aria.ManaMax, _aria.Location) = (60, 60, new Point3D(10, 10, 0));
        (_bran.Hits, _bran.HitsMax, _bran.Location) = (20, 50, new Point3D(12, 10, 0));
        (_bran.Strength, _bran.Dexterity, _bran.Intelligence) = (50, 50, 50);
        (_bran.Stamina, _bran.StaminaMax, _bran.Mana, _bran.ManaMax) = (50, 50, 50, 50);
        (_aria.Hits, _aria.HitsMax) = (40, 40);
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([_backpack]);

        for (uint serial = 0x40000100; serial < 0x40000180; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        var directories = new DirectoriesConfig(root, ["data", "templates"]);
        var shipped = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        _templates = new(new StubDataLoaderService().With(shipped));
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(shipped)).LoadDataAsync()).Entities;
        var catalog = new SpellCatalogService(new StubDataLoaderService().With(spells.ToArray()), _templates);
        var gumpTemplates = (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();

        var mobileTemplates = new MobileTemplateService(
            new StubDataLoaderService().With(
                Summons.Select(id => new MobileTemplate { Id = id, ControlSlots = SlotsOf(id), ScriptId = "monster" })
                    .Concat(Animals.Select(id => new MobileTemplate { Id = id }))
                    .Append(new MobileTemplate { Id = "orc" })
                    .ToArray()
            )
        );

        WriteScripts(root);
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new BodyContent { Body = new Body(HumanBody), Type = BodyType.Human },
            new BodyContent { Body = new Body(OrcBody), Type = BodyType.Monster }
        );
        _skills.Result = true;
        _state.Skills.AddRange(
            [
                new MobileSkill { Skill = SkillType.Magery, Base = 1000 },
                new MobileSkill { Skill = SkillType.EvaluatingIntelligence, Base = 500 }
            ]
        );
        _bonuses = new(_state, _fixture.Sessions, _fixture.Sender, _timers, _fixture.Mobiles);
        _paralysis = new(_state, _timers, _time);
        _disguise = new(_state, _timers, _time);
        var taming = new TamingService(new StubDataLoaderService().With<TamingCreature>());
        _pets = new(_fixture.Mobiles, _items, taming, new PetsConfig(), _time, templates: mobileTemplates);
        GumpScriptService? gumpScripts = null;

        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<IItemTemplateService>(_templates);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<ISkillService>(_skills);
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
        _container.RegisterInstance<IItemFactoryService>(new FakeItemFactoryService(_templates, new FakeTileDataService()));
        _container.RegisterInstance<IItemSerialPool>(_serials);
        _container.RegisterInstance<ITileDataService>(new FakeTileDataService().Item(0x0E75, TileFlagType.Container, 0).Item(0x0692, TileFlagType.Impassable, 20)
            .Item(0x0082, TileFlagType.Impassable, 20)
        );
        _container.RegisterInstance<IContainerCapacityService>(_capacity);
        _container.RegisterInstance<IInventoryMutationGuard>(_guard);
        _container.RegisterInstance<TimeProvider>(_time);
        _container.RegisterInstance<IMapService>(_map);
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(
            new RegionService(
                new StubDataLoaderService().With(
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Sealed", GateIn = false, GateOut = false, Mark = false,
                        Areas = [new RegionAreaContent { X1 = 40, Y1 = 40, X2 = 60, Y2 = 60 }]
                    },
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Town", Guarded = true, RuneName = "Town Square",
                        Areas = [new RegionAreaContent { X1 = 18, Y1 = 0, X2 = 24, Y2 = 12 }]
                    },
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Shut", GateOut = false, Mark = false,
                        Areas = [new RegionAreaContent { X1 = 0, Y1 = 40, X2 = 20, Y2 = 60 }]
                    }
                )
            )
        );
        _container.RegisterInstance<IUseService>(_uses);
        _container.RegisterInstance<IItemTimerService>(new LateItemTimerService(() => _itemTimers));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IMovementService>(_movement);
        _container.RegisterInstance<IDeathService>(new StubDeathService());
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterInstance<ICombatService>(_combat);
        _container.RegisterInstance<IStatBonusService>(_bonuses);
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        _container.RegisterInstance<IParalysisService>(_paralysis);
        _container.RegisterInstance<IDisguiseService>(_disguise);
        _container.RegisterInstance<INameService>(
            new NameService(
                new StubDataLoaderService().With(
                    new NameList { Id = "male", Names = ["Alaric"] },
                    new NameList { Id = "female", Names = ["Beatrix"] }
                )
            )
        );
        _container.RegisterInstance<INpcService>(_npcService);
        _container.RegisterInstance<IMobileTemplateService>(mobileTemplates);
        _container.RegisterInstance<IPetService>(_pets);
        _container.RegisterInstance<ITamingService>(taming);
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance(new CombatConfig());
        _container.RegisterInstance<ISpellCatalogService>(catalog);
        _container.Register<IPoisonService, PoisonService>(Reuse.Singleton, made: Parameters.Of.Type<Random>(_ => null));
        _container.Register<ISpellbookService, SpellbookService>(Reuse.Singleton);
        _container.RegisterDelegate<ISpellCastService>(_ => _casts);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<CombatModule>();
        _container.AddScriptModule<SpellModule>();
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<PetModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<DiceModule>();
        _container.RegisterScriptEnum<BodyType>();
        _container.RegisterScriptEnum<MonsterAnimationType>();
        _container.RegisterScriptEnum<Moongate.Server.Ultima.Types.Speech.SpeechKeywordType>();
        _container.RegisterScriptEnum<PetObeyResultType>();
        _container.RegisterScriptEnum<PetFeedResultType>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.RegisterInstance<IDataLoaderService>(data);
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _books = (SpellbookService)_container.Resolve<ISpellbookService>();
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
        var spellScripts = new SpellScriptService(_engine, _loop, options);
        await spellScripts.StartAsync();
        _npcScripts = new(_engine, mobileTemplates, _loop, options);
        await _npcScripts.StartAsync();
        _itemScripts = new(_engine, _templates, _loop, options);
        await _itemScripts.StartAsync();
        _itemTimers = new(_timers, new ItemTimerQueue(_time), _items, _itemScripts, _time);
        await _itemTimers.StartAsync();
        _casts = new(
            catalog,
            _books,
            spellScripts,
            _items,
            _templates,
            _container.Resolve<IItemHandlingService>(),
            _fixture.Mobiles,
            _state,
            _fixture.Sessions,
            _targets,
            _speech,
            _effects,
            _view,
            _skills,
            _sight,
            _timers,
            _time
        );

        _book = Carry("spellbook_full", 0x0EFA, 1);

        // Ten of each reagent, as the book's cast takes them.
        foreach (var (template, graphic) in new[]
                 {
                     ("0x0f7a_black_pearl", 0x0F7A), ("0x0f7b_blood_moss", 0x0F7B), ("0x0f84_garlic", 0x0F84),
                     ("0x0f85_ginseng", 0x0F85), ("0x0f86_mandrake_root", 0x0F86), ("0x0f88_nightshade", 0x0F88),
                     ("0x0f8c_sulfurous_ash", 0x0F8C), ("0x0f8d_spider_silk", 0x0F8D)
                 })
        {
            Carry(template, graphic, 10);
        }

        Roll(pick: 0, roll: 1);
        _loop.DeferTryPost = true;
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    [Fact]
    public void MindBlast_DoesHalfTheGapOfTheStatsOfTheTarget_HalfASecondLater()
    {
        (_bran.Strength, _bran.Dexterity, _bran.Intelligence) = (100, 40, 40);

        Cast("mind_blast");

        Assert.Empty(_errors);
        Assert.Equal(46, _aria.Mana);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Empty(_combat.Harmed);

        FireAfter(0.5);

        // (100 - 40) / 2 x 1.1 = 33, under the cap of 45.
        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _bran, 33), (harm.Attacker, harm.Target, harm.Damage));
    }

    [Fact]
    public void MindBlast_IsCappedAtFortyFive_AndHalvedWhenTheTargetResists()
    {
        (_bran.Strength, _bran.Dexterity, _bran.Intelligence) = (150, 0, 0);
        Cast("mind_blast");
        FireAfter(0.5);
        Assert.Equal(45, Assert.Single(_combat.Harmed).Damage);

        _time.Advance(TimeSpan.FromSeconds(2));
        _combat.Harmed.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 1000 });
        Roll(pick: 0, roll: 0);
        (_bran.Strength, _bran.Dexterity, _bran.Intelligence) = (100, 40, 40);

        Cast("mind_blast");
        FireAfter(0.5);

        // (100 - 40) / 2 x (1 + (50 - 100) / 200) = 22.5, halved: 11.
        Assert.Equal(11, Assert.Single(_combat.Harmed).Damage);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void Paralyze_FreezesTheTarget_ForSevenSecondsAndAFifthOfASecondAPoint()
    {
        Cast("paralyze");

        Assert.Empty(_errors);
        Assert.True(_bran.Frozen);
        Assert.True(_paralysis.IsParalyzed(_bran));
        Assert.Equal(TimeSpan.FromSeconds(27), _timers.Timers.Single(timer => timer.Name == "paralysis").Interval);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Equal(46, _aria.Mana);
        Assert.Contains(_effects.On, shown => shown.Target == _bran.Id);
    }

    [Fact]
    public void Paralyze_ThatTheTargetResists_LastsThreeQuartersOfIt()
    {
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 1000 });
        Roll(pick: 0, roll: 0);

        Cast("paralyze");

        Assert.Equal(TimeSpan.FromSeconds(20.25), _timers.Timers.Single(timer => timer.Name == "paralysis").Interval);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void Paralyze_OfATargetAlreadyFrozen_IsRefusedBeforeAnythingIsSpent()
    {
        _bran.Frozen = true;

        Cast("paralyze");

        Assert.Contains(1061923, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.DoesNotContain(_timers.Timers, timer => timer.Name == "paralysis");
    }

    [Fact]
    public void Paralyze_RuinsTheCastOfTheTarget_AndAFrozenCasterCannotCast()
    {
        _paralysis.Paralyze(_aria, TimeSpan.FromSeconds(10));

        Assert.False(_casts.CastFromBook(_aria, SpellId("paralyze")));
        Assert.Contains(502643, ToldTo(_aria));
    }

    [Fact]
    public void MagicReflection_WrapsTheCaster_AndASecondCastIsRefusedBeforeAnythingIsSpent()
    {
        CastNoTarget("magic_reflection");

        Assert.Empty(_errors);
        Assert.True(_aria.GetProp("magic.reflect", false));
        Assert.Equal(46, _aria.Mana);

        _time.Advance(TimeSpan.FromSeconds(2));
        CastNoTarget("magic_reflection");

        Assert.Contains(1005559, ToldTo(_aria));
        Assert.Equal(46, _aria.Mana);
    }

    [Fact]
    public void AnEnergyBolt_AtAWearerOfMagicReflection_HurtsItsCaster_NoOneToBlame_AndTheWearerIsAggressed()
    {
        _bran.SetProp("magic.reflect", true);

        Cast("energy_bolt");
        FireAfter(0.5);

        Assert.Empty(_errors);
        Assert.False(_bran.TryGetProp<bool>("magic.reflect", out _));
        // The caster is the aggressor of the wearer it aimed at, and hurts itself: no one is a criminal for it.
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _aria), (harm.Attacker, harm.Target));
    }

    [Fact]
    public void AParalyze_AtAWearerOfMagicReflection_FreezesItsCaster_ForTheDurationOfItsOwnMagery()
    {
        _bran.SetProp("magic.reflect", true);

        Cast("paralyze");

        Assert.Empty(_errors);
        Assert.True(_aria.Frozen);
        Assert.False(_bran.Frozen);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
    }

    [Fact]
    public void AManaVampire_AtAWearerOfMagicReflection_TakesNothingFromTheWearer()
    {
        _bran.SetProp("magic.reflect", true);
        Roll(pick: 0, roll: 1);

        Cast("mana_vampire");

        Assert.Equal(50, _bran.Mana);
        Assert.Equal(20, _aria.Mana);
    }

    [Fact]
    public void ACurse_AtAWearerOfMagicReflection_CursesItsCaster_AndTheWearerIsAggressed()
    {
        _bran.SetProp("magic.reflect", true);

        Cast("curse");

        Assert.Empty(_errors);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.True(_bonuses.Bonus(_aria, StatBonusType.Strength) < 0);
        Assert.Equal(0, _bonuses.Bonus(_bran, StatBonusType.Strength));
    }

    [Fact]
    public void DispelField_TakesAPieceOfAFieldAway_InAPuff()
    {
        var piece = Ground("magic_poison_field_ew", 0x3915, new Point3D(13, 10, 0));

        Cast("dispel_field", piece.Id);

        Assert.Empty(_errors);
        Assert.False(_items.TryGet(piece.Id, out _));
        Assert.Equal(46, _aria.Mana);
        Assert.Contains(_effects.At, shown => shown.Options.Graphic == 0x376A);
    }

    [Fact]
    public void DispelField_OfAGateOfGateTravel_TakesTheOtherGateToo()
    {
        var one = Ground("magic_gate", 0x0F6C, new Point3D(13, 10, 0));
        var other = Ground("magic_gate", 0x0F6C, new Point3D(30, 30, 0));
        one.SetProp("gate.partner", (long)other.Id.Value);
        other.SetProp("gate.partner", (long)one.Id.Value);

        Cast("dispel_field", one.Id);

        Assert.False(_items.TryGet(one.Id, out _));
        Assert.False(_items.TryGet(other.Id, out _));
    }

    [Fact]
    public void DispelField_OfAPlainMoongateOrAnyItem_IsRefusedBeforeAnythingIsSpent()
    {
        var gate = Ground("moongate", 0x0F6C, new Point3D(13, 10, 0));
        var stone = Ground("0x0e40_stone", 0x1363, new Point3D(13, 11, 0));

        Cast("dispel_field", gate.Id);
        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("dispel_field", stone.Id);

        Assert.Equal([1005047, 1005049], ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.True(_items.TryGet(gate.Id, out _));
    }

    [Fact]
    public void PoisonField_RaisesFivePieces_ThatPoisonWhoStepsOnThem_ButNotABluePlayer()
    {
        _movement.SpawnZ = (_, _) => 0;
        var orc = Npc(0x200, OrcBody);

        CastAt("poison_field", new Point3D(14, 10, 0));

        Assert.Empty(_errors);
        var pieces = PiecesNear(14, 10, "magic_poison_field_ns");
        Assert.Equal(5, pieces.Count);
        Assert.Equal(46, _aria.Mana);
        Assert.All(pieces, piece => Assert.Equal(TimeSpan.FromSeconds(20), _itemTimers.Remaining(piece, "tick") * 20));
        var piece = pieces.Single(found => found.GroundLocation!.Value.Y == 10);
        Place(orc, new Point3D(14, 10, 0));
        Place(_bran, new Point3D(14, 10, 0));

        _itemScripts.Run(piece, "on_npc_move_over", (long)orc.Id.Value);
        _itemScripts.Run(piece, "on_move_over", (long)_bran.Id.Value);

        Assert.Equal(1, _container.Resolve<IPoisonService>().LevelOf(orc));
        Assert.Null(_container.Resolve<IPoisonService>().LevelOf(_bran));
        Assert.Contains((_aria, orc), _combat.Aggressed);
    }

    [Fact]
    public void ParalyzeField_FreezesWhoStepsOnIt_ForSevenSecondsAndAFifthOfASecondAPoint()
    {
        _movement.SpawnZ = (_, _) => 0;
        var orc = Npc(0x200, OrcBody);
        CastAt("paralyze_field", new Point3D(14, 10, 0));
        var piece = PiecesNear(14, 10, "magic_paralyze_field_ns").Single(found => found.GroundLocation!.Value.Y == 10);
        Place(orc, new Point3D(14, 10, 0));

        _itemScripts.Run(piece, "on_npc_move_over", (long)orc.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal(5, PiecesNear(14, 10, "magic_paralyze_field_ns").Count);
        Assert.True(orc.Frozen);
        Assert.Equal(TimeSpan.FromSeconds(27), _timers.Timers.Single(timer => timer.Name == "paralysis").Interval);
        Assert.Equal(40, _aria.Mana);

        // The pieces go away after twenty seconds.
        RunItemTimers(TimeSpan.FromSeconds(21));

        Assert.Empty(PiecesNear(14, 10, "magic_paralyze_field_ns"));
    }

    [Fact]
    public void EnergyField_RaisesFivePieces_ForTwoSecondsAndAQuarterOfASecondAPoint()
    {
        _movement.SpawnZ = (_, _) => 0;

        CastAt("energy_field", new Point3D(14, 10, 0));

        Assert.Empty(_errors);
        var pieces = PiecesNear(14, 10, "magic_energy_field_ns");
        Assert.Equal(5, pieces.Count);
        Assert.Equal(60 - 40, _aria.Mana);
        Assert.All(pieces, piece => Assert.Equal(TimeSpan.FromSeconds(30), _itemTimers.Remaining(piece, "expire")));
    }

    [Theory]
    [InlineData("poison_field")]
    [InlineData("paralyze_field")]
    [InlineData("energy_field")]
    public void AField_AimedAtAGuardedTown_IsRefusedBeforeAnythingIsSpent(string key)
    {
        _movement.SpawnZ = (_, _) => 0;

        CastAt(key, new Point3D(20, 10, 0));

        Assert.Empty(_errors);
        Assert.Contains(500946, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.Empty(_fixture.Sectors.GetItemsInRange(_aria.Map, new Point3D(20, 10, 0), 6));
    }

    [Fact]
    public void BladeSpirits_CallsASpiritAtThePlace_ThatFollowsTheCasterAndGoesAwayAfterEightyToOneHundredAndNineteenSeconds()
    {
        _movement.SpawnZ = (_, _) => 0;
        var spirit = NextSummon("bladespirit_summon");

        CastAt("blade_spirits", new Point3D(13, 12, 0));

        Assert.Empty(_errors);
        Assert.Equal(("bladespirit_summon", new Point3D(13, 12, 0)), (_npcService.Spawns.Single().TemplateId, _npcService.Spawns.Single().Location));
        Assert.Equal(46, _aria.Mana);
        Assert.Equal(
            (Aria, "guard", 1),
            (spirit.GetProp("owner", 0L), spirit.GetProp<string>("pet.order"), _pets.Followers(_aria))
        );
        // Nobody commands a blade spirit: it guards its master and does not hear the words of a pet.
        Assert.True(spirit.GetProp("pet.uncontrollable", false));
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds() + 80, spirit.GetProp<long>("summon.until"));
        Assert.Contains(_timers.Timers, timer => timer.Name.StartsWith("lua-timer:") && timer.Interval == TimeSpan.FromSeconds(80));
        Assert.Equal((0L, 20L), (spirit.GetProp<long>("summon.difficulty"), spirit.GetProp<long>("summon.focus")));
    }

    [Fact]
    public void ASummon_GoesAwayWhenItsTimeIsUp_InAPuff_AndOnlyOnce()
    {
        _movement.SpawnZ = (_, _) => 0;
        var spirit = NextSummon("bladespirit_summon");
        CastAt("blade_spirits", new Point3D(13, 12, 0));
        _effects.At.Clear();

        FireAfter(80);
        FireAfter(80);

        Assert.Single(_effects.At, shown => shown.Options.Graphic == 0x3728);
        Assert.False(spirit.TryGetProp<long>("summon.until", out _));
        SpinWait.SpinUntil(() => _npcService.Removals.Count > 0, TimeSpan.FromSeconds(2));
        Assert.Equal([spirit.Id], _npcService.Removals);
    }

    [Fact]
    public void ASummon_ThatThinksAfterItsTimeIsUp_GoesAwayInAPuff_AndItsMasterHasOneFollowerLess()
    {
        var spirit = Summoned(0x700, "bladespirit_summon", Aria, seconds: 10);
        _time.Advance(TimeSpan.FromSeconds(11));

        _npcScripts.Think(spirit);

        Assert.Empty(_errors);
        Assert.False(spirit.TryGetProp<long>("summon.until", out _));
        Assert.Contains(_effects.At, shown => shown.Options.Graphic == 0x3728);
        SpinWait.SpinUntil(() => _npcService.Removals.Count > 0, TimeSpan.FromSeconds(2));
        Assert.Equal([spirit.Id], _npcService.Removals);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("gone")]
    [InlineData("released")]
    public void ASummon_WhoseMasterIsDeadGoneOrLetItGo_GoesAwayAtItsNextThink(string why)
    {
        var spirit = Summoned(0x700, "bladespirit_summon", Aria);

        switch (why)
        {
            case "dead":
                _aria.Body = 0x0192;

                break;
            case "gone":
                _fixture.Mobiles.LeaveWorld(_aria.Id);

                break;
            default:
                spirit.RemoveProp("owner");

                break;
        }

        _npcScripts.Think(spirit);

        Assert.Empty(_errors);
        Assert.False(spirit.TryGetProp<long>("summon.until", out _));
    }

    [Fact]
    public void ASummon_WhoseMasterIsThere_AndTimeIsLeft_StaysAtItsNextThink()
    {
        var spirit = Summoned(0x700, "bladespirit_summon", Aria);

        _npcScripts.Think(spirit);

        Assert.Empty(_errors);
        Assert.True(spirit.TryGetProp<long>("summon.until", out _));
        Assert.Empty(_npcService.Removals);
    }

    [Fact]
    public void ASummon_ThatDies_LeavesNoCorpse()
    {
        var spirit = Summoned(0x700, "bladespirit_summon", Aria);
        var corpse = Ground("corpse", 0x2006, spirit.Location);

        _npcScripts.Run(spirit, "on_death", (long)corpse.Id.Value, 0L);

        Assert.Empty(_errors);
        Assert.False(_items.TryGet(corpse.Id, out _));
    }

    [Fact]
    public void ASummon_IsRefusedBeforeAnythingIsSpent_WhenItsSlotsDoNotFitTheFollowers()
    {
        _movement.SpawnZ = (_, _) => 0;

        // Five followers, and one more for a blade spirit: more than five.
        for (var index = 0; index < 5; index++)
        {
            Npc(0x600 + (uint)index, OrcBody).SetProp("owner", Aria);
        }

        CastAt("blade_spirits", new Point3D(13, 12, 0));

        Assert.Contains(1049645, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.Empty(_npcService.Spawns);
    }

    [Fact]
    public void BladeSpirits_AtAGuardedTown_OrAtAFilledPlace_IsRefusedBeforeAnythingIsSpent()
    {
        _movement.SpawnZ = (_, _) => 0;

        CastAt("blade_spirits", new Point3D(20, 10, 0));
        _time.Advance(TimeSpan.FromSeconds(2));
        Place(_bran, new Point3D(14, 10, 0));
        CastAt("blade_spirits", new Point3D(14, 10, 0));

        Assert.Equal([500946, 501942], ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.Empty(_npcService.Spawns);
    }

    [Fact]
    public void SummonCreature_CallsAnAnimalOfTheList_ForAsManySecondsAsTheMagery()
    {
        _movement.SpawnZ = (_, _) => 0;
        var animal = NextSummon();
        Roll(pick: 4);

        CastNoTarget("summon_creature");

        Assert.Empty(_errors);
        Assert.Equal("horse", Assert.Single(_npcService.Spawns).TemplateId);
        Assert.Equal(46, _aria.Mana);
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds() + 100, animal.GetProp<long>("summon.until"));
        Assert.Equal(Aria, animal.GetProp("owner", 0L));
    }

    [Theory]
    [InlineData("summon_air_elemental", "airele_summon", 117.5, 45.0)]
    [InlineData("summon_earth_elemental", "earthele_summon", 117.5, 45.0)]
    [InlineData("summon_fire_elemental", "firele_summon", 117.5, 45.0)]
    [InlineData("summon_water_elemental", "waterele_summon", 117.5, 45.0)]
    [InlineData("summon_daemon", "daemon_summon", 125.0, 45.0)]
    public void TheEighthCircleSummons_CallTheirCreature_BesideTheCaster_ForAsManySecondsAsTheMagery(
        string key,
        string template,
        double difficulty,
        double focus
    )
    {
        _movement.SpawnZ = (_, _) => 0;
        var creature = NextSummon(template);

        CastNoTarget(key);

        Assert.Empty(_errors);
        Assert.Equal(template, Assert.Single(_npcService.Spawns).TemplateId);
        Assert.Equal(10, _aria.Mana);
        Assert.Equal(Aria, creature.GetProp("owner", 0L));
        Assert.Equal((difficulty, focus), (creature.GetProp<double>("summon.difficulty"), creature.GetProp<double>("summon.focus")));
        Assert.Equal(SlotsOf(template), _pets.Followers(_aria));
    }

    [Fact]
    public void SummonFireElemental_IsRefusedWhenTheCasterHasTwoFollowers_AsItCountsForFour()
    {
        Npc(0x600, OrcBody).SetProp("owner", Aria);
        Npc(0x601, OrcBody).SetProp("owner", Aria);

        CastNoTarget("summon_fire_elemental");

        Assert.Contains(1049645, ToldTo(_aria));
        Assert.Equal(50 + 10, _aria.Mana);
        Assert.Empty(_npcService.Spawns);
    }

    [Fact]
    public void SummonDaemon_CostsSeventyPointsOfKarma()
    {
        _movement.SpawnZ = (_, _) => 0;
        _aria.Karma = 500;
        NextSummon();

        CastNoTarget("summon_daemon");

        Assert.Equal(430, _aria.Karma);
    }

    [Fact]
    public void Dispel_UndoesASummonedCreatureTheMageryBeatsTheDifficultyOf_InAPuff()
    {
        var spirit = Summoned(0x700, "bladespirit_summon", Bran);
        spirit.SetProp("summon.difficulty", 0.0);
        spirit.SetProp("summon.focus", 20.0);
        _effects.At.Clear();

        Cast("dispel", spirit.Id);

        Assert.Empty(_errors);
        Assert.Equal(40, _aria.Mana);
        Assert.Contains(_effects.At, shown => shown.Options.Graphic == 0x3728);
        Assert.False(spirit.TryGetProp<long>("summon.until", out _));
        Assert.Contains((_aria, spirit), _combat.Aggressed);
    }

    [Fact]
    public void Dispel_OfACreatureThatHoldsOut_ShowsItAndTellsTheCaster()
    {
        var daemon = Summoned(0x700, "daemon_summon", Bran);
        daemon.SetProp("summon.difficulty", 125.0);
        daemon.SetProp("summon.focus", 45.0);
        Roll(pick: 0, roll: 0.5);

        Cast("dispel", daemon.Id);

        // (50 + 100 x (100 - 125) / 90) / 100 = 0.22, and the roll is 0.5.
        Assert.Contains(1010084, ToldTo(_aria));
        Assert.True(daemon.TryGetProp<long>("summon.until", out _));
        Assert.Contains(_effects.On, shown => shown.Target == daemon.Id && shown.Options.Graphic == 0x3779);
    }

    [Fact]
    public void Dispel_OfWhatIsNoSummon_IsRefusedBeforeAnythingIsSpent()
    {
        var orc = Npc(0x200, OrcBody);

        Cast("dispel", orc.Id);

        Assert.Contains(1005049, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void MassDispel_UndoesTheSummonsAroundThePlace_EachByItsOwnChance_AndAggressesTheOnesThatHoldOut()
    {
        var spirit = Summoned(0x700, "bladespirit_summon", Bran);
        spirit.SetProp("summon.difficulty", 0.0);
        spirit.SetProp("summon.focus", 20.0);
        var daemon = Summoned(0x701, "daemon_summon", Bran);
        daemon.SetProp("summon.difficulty", 125.0);
        daemon.SetProp("summon.focus", 45.0);
        Roll(pick: 0, roll: 0.5);

        CastAt("mass_dispel", new Point3D(12, 11, 0));

        Assert.Empty(_errors);
        Assert.Equal(60 - 40, _aria.Mana);
        Assert.False(spirit.TryGetProp<long>("summon.until", out _));
        Assert.True(daemon.TryGetProp<long>("summon.until", out _));
        Assert.Contains((_aria, daemon), _combat.Aggressed);
    }

    [Fact]
    public void MassDispel_WithNoSummonAround_IsRefusedBeforeAnythingIsSpent()
    {
        Npc(0x200, OrcBody);

        CastAt("mass_dispel", new Point3D(12, 11, 0));

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void Incognito_GivesTheCasterAHueAndANameOfItsSex_ForOneAndTwoTenthsOfASecondAPoint()
    {
        _aria.Gender = GenderType.Female;
        CastNoTarget("incognito");

        Assert.Empty(_errors);
        Assert.Equal("Beatrix", _aria.Name);
        Assert.Equal((ushort)0x3EA, _aria.SkinHue.Value);
        Assert.Equal(46, _aria.Mana);
        Assert.Equal(TimeSpan.FromSeconds(120), _timers.Timers.Single(timer => timer.Name == "disguise").Interval);

        _timers.Fire(_timers.Timers.Single(timer => timer.Name == "disguise").Id);

        Assert.Equal("Aria", _aria.Name);
    }

    [Fact]
    public void Incognito_OfADisguisedCaster_IsRefusedBeforeAnythingIsSpent()
    {
        CastNoTarget("incognito");
        _time.Advance(TimeSpan.FromSeconds(2));

        CastNoTarget("incognito");

        Assert.Contains(1005559, ToldTo(_aria));
        Assert.Equal(46, _aria.Mana);
    }

    [Fact]
    public void Polymorph_OpensTheListOfFormsAtFirst_AndSpendsNothing()
    {
        CastNoTarget("polymorph");

        Assert.Empty(_errors);
        Assert.Equal(60, _aria.Mana);
        Assert.Equal("polymorph_forms", Assert.Single(_gumps.Opened).Gump.Id);
        Assert.Equal(HumanBody, _aria.Body);
    }

    [Fact]
    public void Polymorph_ThatTheListGaveAForm_ChangesTheBody_UntilTheTimeIsUp()
    {
        CastNoTarget("polymorph");
        _time.Advance(TimeSpan.FromSeconds(2));

        // The button of the list: its script is loaded, and the handler of the form is called with the player.
        _engine.LoadFile("gumps/polymorph_forms.lua");
        _engine.CallMember("gumps/polymorph_forms.lua", "polymorph_forms", "wolf", (long)_aria.Id.Value, 1L);
        FireDelay();
        Drain();

        Assert.Empty(_errors);
        Assert.Equal(0xE1, _aria.Body);
        Assert.Equal(20, _aria.Mana);
        Assert.Equal(TimeSpan.FromSeconds(120), _timers.Timers.Single(timer => timer.Name == "disguise").Interval);

        _timers.Fire(_timers.Timers.Single(timer => timer.Name == "disguise").Id);

        Assert.Equal(HumanBody, _aria.Body);
    }

    [Fact]
    public void Invisibility_HidesTheTarget_ForOneAndTwoTenthsOfASecondAPoint_AndShowsItAgain()
    {
        Cast("invisibility");

        Assert.Empty(_errors);
        Assert.True(_bran.Hidden);
        // No steps of stealth: the first step it takes shows it, as it does for a Hiding without Stealth.
        Assert.Equal(0, _bran.AllowedStealthSteps);
        Assert.Equal(40, _aria.Mana);
        Assert.Contains(_combat.Stopped, who => who == _bran);

        _time.Advance(TimeSpan.FromSeconds(120));
        FireAfter(120);

        Assert.False(_bran.Hidden);
    }

    [Fact]
    public void Invisibility_ThatTheTargetShowedAndHidAgainByItself_IsNotEndedByTheOldTimer()
    {
        Cast("invisibility");
        _state.SetHidden(_bran, false);
        _time.Advance(TimeSpan.FromSeconds(60));
        _bran.SetProp("magic.invisible_until", _time.GetUtcNow().ToUnixTimeSeconds() + 200);
        _state.SetHidden(_bran, true);

        FireAfter(120);

        Assert.True(_bran.Hidden);
    }

    [Fact]
    public void Invisibility_OnAnInvulnerable_IsRefusedBeforeAnythingIsSpent()
    {
        _bran.Notoriety = NotorietyType.Invulnerable;

        Cast("invisibility");

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.False(_bran.Hidden);
    }

    [Fact]
    public void Reveal_ShowsWhoIsHiddenAroundThePlace_WithinOneTileAndAMageryTwentiethOfIt()
    {
        var orc = Npc(0x200, OrcBody);
        var far = Npc(0x201, OrcBody);
        Place(far, new Point3D(30, 30, 0));
        _state.SetHidden(orc, true);
        _state.SetHidden(far, true);

        CastAt("reveal", new Point3D(12, 11, 0));

        Assert.Empty(_errors);
        Assert.False(orc.Hidden);
        Assert.True(far.Hidden);
        Assert.Equal(40, _aria.Mana);
    }

    [Fact]
    public void Reveal_WithNoOneHiddenThere_IsRefusedBeforeAnythingIsSpent()
    {
        CastAt("reveal", new Point3D(12, 11, 0));

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void EnergyBolt_FliesToTheTarget_AndHurtsItHalfASecondLater_ForTwentyFourToFortyOne()
    {
        Roll(pick: 30);

        Cast("energy_bolt");

        Assert.Empty(_errors);
        Assert.Equal(40, _aria.Mana);
        Assert.Contains(_effects.Moving, moving => moving.Source == _aria.Id && moving.Target == _bran.Id && moving.Options.Graphic == 0x379F);
        Assert.Empty(_combat.Harmed);

        FireAfter(0.5);

        // 30 x 1.1 = 33.
        Assert.Equal(33, Assert.Single(_combat.Harmed).Damage);
    }

    [Fact]
    public void Explosion_BlowsTheTargetUp_TwoAndAHalfSecondsLater_UnlessItIsDead()
    {
        Roll(pick: 30);

        Cast("explosion");

        Assert.Empty(_errors);
        Assert.Equal(40, _aria.Mana);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Empty(_combat.Harmed);

        FireAfter(2.5);

        // 30 x 1.1 = 33.
        Assert.Equal(33, Assert.Single(_combat.Harmed).Damage);
        Assert.Contains(_effects.On, shown => shown.Target == _bran.Id && shown.Options.Graphic == 0x36BD);
    }

    [Fact]
    public void Explosion_OfATargetThatDiedMeanwhile_HurtsNoOne()
    {
        Cast("explosion");
        _bran.Body = 0x0192;

        FireAfter(2.5);

        Assert.Empty(_combat.Harmed);
    }

    [Fact]
    public void FlameStrike_DoesTwentySevenToFortyEight_ThreeFifthsWhenTheTargetResists()
    {
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 1000 });
        Roll(pick: 40, roll: 0);

        Cast("flame_strike");
        FireAfter(0.5);

        // 40 x 0.6 x (1 + (50 - 100) / 200) = 18.
        Assert.Empty(_errors);
        Assert.Equal(18, Assert.Single(_combat.Harmed).Damage);
        Assert.Equal(60 - 40, _aria.Mana);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void ManaVampire_GivesTheManaOfTheTargetToTheCaster_UnlessItResists()
    {
        _aria.ManaMax = 100;
        Roll(pick: 0, roll: 1);

        Cast("mana_vampire");

        // The chance to resist is 98 per cent, and a roll of 1 is above it: the target loses its 50 points.
        Assert.Empty(_errors);
        Assert.Equal(0, _bran.Mana);
        Assert.Equal(60 - 40 + 50, _aria.Mana);
    }

    [Fact]
    public void ManaVampire_ThatTheTargetResists_TakesNothing_AndTellsTheTarget()
    {
        Roll(pick: 0, roll: 0);

        Cast("mana_vampire");

        Assert.Equal(50, _bran.Mana);
        Assert.Equal(20, _aria.Mana);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Theory]
    [InlineData("clumsy")]
    [InlineData("weaken")]
    [InlineData("feeblemind")]
    [InlineData("curse")]
    [InlineData("poison")]
    [InlineData("mana_drain")]
    public void ACurseAPoisonOrAManaDrain_FreesAParalyzedTarget(string key)
    {
        _paralysis.Paralyze(_bran, TimeSpan.FromSeconds(20));
        Roll(pick: 0, roll: 0);

        Cast(key);

        Assert.Empty(_errors);
        Assert.False(_bran.Frozen);
        Assert.False(_paralysis.IsParalyzed(_bran));
    }

    [Fact]
    public void ManaVampire_FreesAParalyzedTarget_AndRuinsItsCast()
    {
        _paralysis.Paralyze(_bran, TimeSpan.FromSeconds(20));
        Roll(pick: 0, roll: 0);

        Cast("mana_vampire");

        Assert.False(_bran.Frozen);
    }

    [Fact]
    public void ChainLightning_StrikesEveryoneAroundThePlace_SharingTheDamageAboveTwo()
    {
        var one = Npc(0x200, OrcBody);
        var two = Npc(0x201, OrcBody);
        var three = Npc(0x202, OrcBody);
        Roll(pick: 30);

        CastAt("chain_lightning", new Point3D(12, 11, 0));

        Assert.Empty(_errors);
        Assert.Equal(60 - 40, _aria.Mana);
        Assert.Equal(3, _combat.Aggressed.Count);
        Assert.Equal([one, two, three], _combat.Aggressed.Select(pair => pair.Target).OrderBy(who => who.Id.Value));

        FireAfter(0.5);

        // 30 / 3 = 10, x 1.1 x 2 for a monster = 22.
        Assert.Equal([22, 22, 22], _combat.Harmed.Select(harm => harm.Damage));
        Assert.All(_combat.Harmed, harm => Assert.Equal(_aria, harm.Attacker));
    }

    [Fact]
    public void ChainLightning_DoesNotStrikeABluePlayerTheCasterItselfOrAnInvulnerable_AndRefusesWhenNoOneIsLeft()
    {
        var guard = Npc(0x200, OrcBody, NotorietyType.Invulnerable);
        Place(_bran, new Point3D(12, 11, 0));

        CastAt("chain_lightning", new Point3D(12, 11, 0));

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.Empty(_combat.Aggressed);
        Assert.NotNull(guard);
    }

    [Fact]
    public void ChainLightning_AtAGuardedTown_IsRefusedBeforeAnythingIsSpent()
    {
        var orc = Npc(0x200, OrcBody);
        Place(orc, new Point3D(20, 10, 0));

        CastAt("chain_lightning", new Point3D(20, 10, 0));

        Assert.Contains(500946, ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void MeteorSwarm_SharesTheDamageByTheNumberOfTargets_AndAMurdererMayHitABluePlayer()
    {
        _aria.Kills = 100;
        var orc = Npc(0x200, OrcBody);
        Place(_bran, new Point3D(12, 11, 0));
        Roll(pick: 40);

        CastAt("meteor_swarm", new Point3D(12, 11, 0));

        Assert.Empty(_errors);
        Assert.Equal(60 - 40, _aria.Mana);
        Assert.Contains((_aria, orc), _combat.Aggressed);
        Assert.Contains(_effects.Moving, moving => moving.Target == orc.Id && moving.Options.Graphic == 0x36D4);

        FireAfter(0.5);

        // 40 / 2 = 20 each; x 1.1 = 22 for a player, x 2.2 = 44 for a monster.
        Assert.Equal(44, _combat.Harmed.Single(harm => harm.Target == orc).Damage);
    }

    [Fact]
    public void MassCurse_CursesEveryoneAroundThePlace_ButNotTheCasterNorABluePlayer()
    {
        var orc = Npc(0x200, OrcBody);
        Place(_bran, new Point3D(12, 11, 0));

        CastAt("mass_curse", new Point3D(12, 11, 0));

        Assert.Empty(_errors);
        Assert.Equal(60 - 20, _aria.Mana);
        Assert.Equal(-11, _bonuses.Bonus(orc, StatBonusType.Strength));
        Assert.Equal(0, _bonuses.Bonus(_bran, StatBonusType.Strength));
        Assert.Equal(0, _bonuses.Bonus(_aria, StatBonusType.Strength));
        Assert.Equal([(_aria, orc)], _combat.Aggressed);
    }

    [Fact]
    public void MassCurse_WithNoOneToCurse_OrAtAGuardedTown_IsRefusedBeforeAnythingIsSpent()
    {
        CastAt("mass_curse", new Point3D(12, 11, 0));
        _time.Advance(TimeSpan.FromSeconds(2));
        var orc = Npc(0x200, OrcBody);
        Place(orc, new Point3D(20, 10, 0));
        CastAt("mass_curse", new Point3D(20, 10, 0));

        Assert.Equal([WontWork, 500946], ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void Earthquake_HurtsEveryoneAroundTheCaster_BySixTenthsOfTheirHits_AtLeastTenForACreature()
    {
        var orc = Npc(0x200, OrcBody);
        var rat = Npc(0x201, OrcBody);
        rat.Hits = 5;
        Place(rat, new Point3D(11, 10, 0));

        CastNoTarget("earthquake");

        Assert.Empty(_errors);
        Assert.Equal(60 - 50, _aria.Mana);
        Assert.Equal(
            [(orc, 60), (rat, 10)],
            _combat.Harmed.Select(harm => (harm.Target, harm.Damage)).OrderBy(pair => pair.Target.Id.Value)
        );
        Assert.All(_combat.Harmed, harm => Assert.Equal(_aria, harm.Attacker));
    }

    [Fact]
    public void Earthquake_IsCappedAtSeventyFive_AndRefusedAtATownOrWithNoOneAround()
    {
        var giant = Npc(0x200, OrcBody);
        giant.Hits = 200;
        giant.HitsMax = 200;

        CastNoTarget("earthquake");

        Assert.Equal(75, Assert.Single(_combat.Harmed).Damage);

        _combat.Harmed.Clear();
        _time.Advance(TimeSpan.FromSeconds(2));
        _aria.Mana = 60;
        Place(giant, new Point3D(40, 40, 0));
        CastNoTarget("earthquake");

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Empty(_combat.Harmed);

        _time.Advance(TimeSpan.FromSeconds(2));
        _aria.Mana = 60;
        Place(_aria, new Point3D(20, 10, 0));
        CastNoTarget("earthquake");

        Assert.Contains(500946, ToldTo(_aria));
    }

    [Fact]
    public void EnergyVortex_CallsAVortexAtThePlace_ForEightyToOneHundredAndNineteenSeconds()
    {
        _movement.SpawnZ = (_, _) => 0;
        var vortex = NextSummon();
        Roll(pick: 10);

        CastAt("energy_vortex", new Point3D(14, 12, 0));

        Assert.Empty(_errors);
        Assert.Equal(("energyvortex_summon", new Point3D(14, 12, 0)), (_npcService.Spawns.Single().TemplateId, _npcService.Spawns.Single().Location));
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds() + 90, vortex.GetProp<long>("summon.until"));
        Assert.Equal((80.0, 20.0), (vortex.GetProp<double>("summon.difficulty"), vortex.GetProp<double>("summon.focus")));
        Assert.True(vortex.GetProp("pet.uncontrollable", false));
        Assert.Equal(10, _aria.Mana);
    }

    [Fact]
    public void Resurrection_AsksTheGhostBesideTheCaster_ToComeBack()
    {
        _bran.Body = 0x0192;
        Place(_bran, new Point3D(11, 10, 0));

        Cast("resurrection");

        Assert.Empty(_errors);
        Assert.Equal(10, _aria.Mana);
        Assert.Equal("resurrect", Assert.Single(_gumps.Opened).Gump.Id);
        Assert.Contains(_effects.On, shown => shown.Target == _bran.Id && shown.Options.Graphic == 0x376A);
    }

    [Theory]
    [InlineData(0, 501041)]
    [InlineData(1, 501042)]
    public void Resurrection_OfTheLivingOrOfAGhostTooFar_IsRefusedBeforeAnythingIsSpent(int dead, int told)
    {
        _bran.Body = dead == 1 ? 0x0192 : HumanBody;

        Cast("resurrection");

        Assert.Equal([told], ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void Resurrection_OfTheCasterOrACreature_IsRefusedBeforeAnythingIsSpent()
    {
        var orc = Npc(0x200, OrcBody);

        Cast("resurrection", _aria.Id);
        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("resurrection", orc.Id);

        Assert.Equal([501039, 501043], ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void Mark_MarksTheRuneInThePack_WithThePlaceOfTheCaster_AndItsName()
    {
        _movement.SpawnZ = (_, _) => 0;
        var rune = Rune(0, 0, 0, marked: false);

        Cast("mark", rune.Id);

        Assert.Empty(_errors);
        Assert.Equal(
            (true, 10L, 10L, 0L, (long)MapType.Trammel),
            (
                rune.GetProp("rune.marked", false), rune.GetProp<long>("rune.x"), rune.GetProp<long>("rune.y"),
                rune.GetProp<long>("rune.z"), rune.GetProp<long>("rune.map")
            )
        );
        Assert.Equal("a recall rune for Trammel", rune.Name);
        Assert.Equal(40, _aria.Mana);
    }

    [Fact]
    public void Mark_ARuneMarkedBefore_TakesTheNewPlace_AndAnythingElseOrARegionThatForbidsItIsRefused()
    {
        var rune = Rune(1, 2, 3, marked: true);
        var stone = Carry("0x0e40_stone", 0x1363, 1);

        Cast("mark", rune.Id);
        Assert.Equal(10L, rune.GetProp<long>("rune.x"));

        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("mark", stone.Id);
        Assert.Contains(501797, ToldTo(_aria));
        Assert.Equal(40, _aria.Mana);

        _time.Advance(TimeSpan.FromSeconds(2));
        Place(_aria, new Point3D(50, 50, 0));
        Cast("mark", rune.Id);
        Assert.Contains(501802, ToldTo(_aria));
        Assert.Equal(10L, rune.GetProp<long>("rune.x"));
    }

    [Fact]
    public void GateTravel_OpensAGateAtBothPlaces_ThatLeadToEachOther_AndGoAwayAfterThirtySeconds()
    {
        _movement.SpawnZ = (_, _) => 0;
        var rune = Rune(30, 30, 0, marked: true);

        Cast("gate_travel", rune.Id);

        Assert.Empty(_errors);
        Assert.Equal(60 - 40, _aria.Mana);
        var here = _fixture.Sectors.GetItemsInRange(_aria.Map, new Point3D(10, 10, 0), 0).Single(item => item.TemplateId == "magic_gate");
        var there = _fixture.Sectors.GetItemsInRange(_aria.Map, new Point3D(30, 30, 0), 0).Single(item => item.TemplateId == "magic_gate");
        Assert.Equal((30L, 30L, 0L), (here.GetProp<long>("teleport.x"), here.GetProp<long>("teleport.y"), here.GetProp<long>("teleport.z")));
        Assert.Equal((10L, 10L), (there.GetProp<long>("teleport.x"), there.GetProp<long>("teleport.y")));
        Assert.Equal((long)there.Id.Value, here.GetProp<long>("gate.partner"));
        Assert.Equal(TimeSpan.FromSeconds(30), _itemTimers.Remaining(here, "expire"));

        RunItemTimers(TimeSpan.FromSeconds(31));

        Assert.False(_items.TryGet(here.Id, out _));
        Assert.False(_items.TryGet(there.Id, out _));
    }

    [Fact]
    public void GateTravel_IsRefusedBeforeAnythingIsSpent_ForAnUnmarkedRune_ACriminalAndAPlaceTheRegionForbids()
    {
        _movement.SpawnZ = (_, _) => 0;
        var blank = Rune(0, 0, 0, marked: false);
        var sealedRune = Rune(50, 50, 0, marked: true);
        var near = Rune(30, 30, 0, marked: true);

        Cast("gate_travel", blank.Id);
        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("gate_travel", sealedRune.Id);
        _time.Advance(TimeSpan.FromSeconds(2));
        _aria.Criminal = true;
        Cast("gate_travel", near.Id);

        Assert.Equal([501805, 1019004, 1005561], ToldTo(_aria));
        Assert.Equal(60, _aria.Mana);
    }

    [Fact]
    public void EverySpellOfTheFifthToEighthCircles_HasAScript()
    {
        var catalog = _container.Resolve<ISpellCatalogService>();

        foreach (var id in Enumerable.Range(33, 32))
        {
            Assert.True(catalog.TryGet(id, out var found));
            Assert.Contains(found.Key, Keys);
            Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "spells", found.Key + ".lua")));
        }
    }

    // The item timers need the item scripts, which need the engine, which builds the modules: the module holds this.
    private sealed class LateItemTimerService : IItemTimerService
    {
        private readonly Func<IItemTimerService> _real;

        public LateItemTimerService(Func<IItemTimerService> real)
        {
            _real = real;
        }

        public bool Start(ItemEntity item, string name, TimeSpan delay)
        {
            return _real().Start(item, name, delay);
        }

        public bool Stop(ItemEntity item, string name)
        {
            return _real().Stop(item, name);
        }

        public TimeSpan? Remaining(ItemEntity item, string name)
        {
            return _real().Remaining(item, name);
        }
    }

    private static int SlotsOf(string template)
    {
        return template switch
        {
            "airele_summon" or "earthele_summon" => 2,
            "waterele_summon" => 3,
            "firele_summon" => 4,
            "daemon_summon" => 5,
            _ => 1
        };
    }

    private void WriteScripts(string root)
    {
        var scripts = Path.Combine(root, "scripts");
        _scripts.Write(
            "common/magic.lua",
            File.ReadAllText(Path.Combine(scripts, "common", "magic.lua"))
                .Replace("magic.random = math.random", "magic.random = function() return ROLL end")
        );

        foreach (var key in Keys)
        {
            var text = File.ReadAllText(Path.Combine(scripts, "spells", key + ".lua"));
            var hook = text.Contains(key + ".random = math.random", StringComparison.Ordinal)
                ? $"\n{key}.random = function(low, high) return math.min(math.max(PICK, low), high) end\n"
                : "";
            _scripts.Write($"spells/{key}.lua", text + hook);
        }

        foreach (var name in new[] { "spellbook", "spell_scroll", "magic_field", "moongate" })
        {
            _scripts.Write($"items/{name}.lua", File.ReadAllText(Path.Combine(scripts, "items", name + ".lua")));
        }

        foreach (var name in new[] { "field", "summon", "creature", "pet_orders" })
        {
            _scripts.Write($"common/{name}.lua", File.ReadAllText(Path.Combine(scripts, "common", name + ".lua")));
        }

        _scripts.Write("mobiles/monster.lua", File.ReadAllText(Path.Combine(scripts, "mobiles", "monster.lua")));

        foreach (var name in new[] { "resurrect", "polymorph_forms" })
        {
            _scripts.Write($"gumps/{name}.lua", File.ReadAllText(Path.Combine(scripts, "gumps", name + ".lua")));
        }

        _scripts.Write(
            "test/roll.lua",
            "PICK = 0\nROLL = 1\ntest_roll = {}\nfunction test_roll.set(pick, roll) PICK = pick ROLL = roll end\n"
        );
    }

    // The numbers the dice give: what random picks (clamped into its range) and what a resist is rolled with.
    private void Roll(int pick, double roll = 1)
    {
        _engine.LoadFile("test/roll.lua");
        _engine.CallMember("test/roll.lua", "test_roll", "set", (long)pick, roll);
    }

    private void Cast(string key, Serial? target = null)
    {
        Assert.True(_casts.CastFromBook(_aria, SpellId(key)), key);
        FireDelay();

        if (_targets.Waiting)
        {
            _targets.Answer(TargetResult.ForObject(target ?? _bran.Id));
        }

        Drain();
    }

    private void CastAt(string key, Point3D place)
    {
        Assert.True(_casts.CastFromBook(_aria, SpellId(key)), key);
        FireDelay();

        if (_targets.Waiting)
        {
            _targets.Answer(TargetResult.ForLocation(_aria.Map, place));
        }

        Drain();
    }

    private void CastNoTarget(string key)
    {
        Assert.True(_casts.CastFromBook(_aria, SpellId(key)), key);
        FireDelay();
        Drain();
    }

    // What a script posted to the loop (the callback of a spawn) runs, as the loop does after the work in hand.
    private void Drain()
    {
        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }
    }

    private void FireDelay()
    {
        var delay = _timers.Timers.FirstOrDefault(timer => timer.Name == "spell_cast");

        if (delay is not null)
        {
            _timers.Fire(delay.Id);
            Drain();
        }
    }

    // Fires the timers of the scripts that were set for this long, such as a delayed damage.
    private void FireAfter(double seconds)
    {
        foreach (var timer in _timers.Timers
                     .Where(timer => timer.Name.StartsWith("lua-timer:", StringComparison.Ordinal) &&
                                     Math.Abs(timer.Interval.TotalSeconds - seconds) < 0.001)
                     .ToList())
        {
            _timers.Fire(timer.Id);
        }
    }

    private int SpellId(string key)
    {
        return _container.Resolve<ISpellCatalogService>().TryGetByKey(key, out var spell) ? spell.Id : 0;
    }

    // Moves a mobile and tells the sectors, so that the places around it list it.
    private void Place(MobileEntity who, Point3D place)
    {
        who.Location = place;
        _fixture.Sectors.Move(who);
    }

    private ItemEntity Ground(string template, int graphic, Point3D place)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, _aria.Map, place);

        return item;
    }

    private ItemEntity Rune(int x, int y, int z, bool marked)
    {
        var rune = Carry("recall_rune", 0x1F14, 1);

        if (marked)
        {
            rune.SetProp("rune.marked", true);
            rune.SetProp("rune.x", (long)x);
            rune.SetProp("rune.y", (long)y);
            rune.SetProp("rune.z", (long)z);
            rune.SetProp("rune.map", (long)_aria.Map);
        }

        return rune;
    }

    // The pieces of a field of the template on the ground around a place.
    private List<ItemEntity> PiecesNear(int x, int y, string template)
    {
        return _fixture.Sectors.GetItemsInRange(_aria.Map, new Point3D(x, y, 0), 6)
            .Where(item => item.TemplateId == template)
            .ToList();
    }

    // Time goes by, and the item timer wheel runs what is due.
    private void RunItemTimers(TimeSpan elapsed)
    {
        var wheel = _timers.Timers.Single(timer => timer.Name == ItemTimerService.TimerName);
        var step = TimeSpan.FromSeconds(1);

        for (var passed = TimeSpan.Zero; passed < elapsed; passed += step)
        {
            _time.Advance(step);
            _timers.Fire(wheel.Id);
        }
    }

    private List<int> ToldTo(MobileEntity player)
    {
        return _speech.ToldClilocs.Where(told => told.Player == player).Select(told => told.Cliloc).ToList();
    }

    // A creature that fights for nobody: a monster, of the body of an orc, in sight of the caster.
    private MobileEntity Npc(uint serial, int body, NotorietyType? notoriety = NotorietyType.Murderer)
    {
        var npc = new MobileEntity
        {
            Id = new Serial(serial), Name = "an orc", TemplateId = "orc", Body = body, Map = _aria.Map,
            Location = new Point3D(12, 11, 0), Hits = 100, HitsMax = 100, Notoriety = notoriety
        };
        _fixture.Mobiles.EnterWorld(npc);
        _fixture.Sectors.Move(npc);

        return npc;
    }

    // A creature a spell summoned: owned by the caster, in the world, with the summon props.
    private MobileEntity Summoned(uint serial, string template, long owner, long seconds = 100)
    {
        var npc = Npc(serial, OrcBody, NotorietyType.Innocent);
        npc.TemplateId = template;
        npc.SetProp("owner", owner);
        npc.SetProp("summon.until", _time.GetUtcNow().ToUnixTimeSeconds() + seconds);

        return npc;
    }

    private ItemEntity Carry(string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([item]);

        return item;
    }

    // What the stub spawn service made for the last spell: the NPC enters the world and the callback runs.
    private MobileEntity NextSummon(string template = "orc")
    {
        var made = new MobileEntity
        {
            Id = new Serial(_nextMobile++), Name = "a summon", TemplateId = template, Body = OrcBody, Map = _aria.Map,
            Hits = 50, HitsMax = 50
        };
        _npcService.Spawned = made;
        _fixture.Mobiles.EnterWorld(made);

        return made;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
