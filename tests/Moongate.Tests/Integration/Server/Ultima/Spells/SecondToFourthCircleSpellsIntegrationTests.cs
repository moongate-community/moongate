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
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Spells;

/// <summary>
///     The shipped second, third and fourth circles and Reactive Armor: the scripts of <c>scripts/spells</c>, the field
///     script and <c>scripts/common/magic.lua</c> over the real cast and spellbook services and the shipped spells and item
///     templates, from the cast to the effect.
/// </summary>
public sealed class SecondToFourthCircleSpellsIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Bran = 3;
    private const int HumanBody = 400;
    private const int OrcBody = 17;
    private const int WontWork = 501857;

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
    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private ItemTimerService _itemTimers = null!;
    private ItemService _items = null!;
    private ItemTemplateService _templates = null!;
    private SpellCastService _casts = null!;
    private SpellbookService _books = null!;
    private StatBonusService _bonuses = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bran = null!;
    private ItemEntity _book = null!;
    private uint _next = 0x40000050;

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
        (_aria.Mana, _aria.ManaMax, _aria.Location) = (30, 30, new Point3D(10, 10, 0));
        (_bran.Hits, _bran.HitsMax, _bran.Location) = (20, 50, new Point3D(12, 10, 0));
        (_bran.Strength, _bran.Dexterity, _bran.Intelligence) = (50, 50, 50);
        (_bran.Stamina, _bran.StaminaMax, _bran.Mana, _bran.ManaMax) = (50, 50, 50, 50);
        (_aria.Hits, _aria.HitsMax) = (40, 40);
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([_backpack]);

        for (uint serial = 0x40000100; serial < 0x40000140; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        var directories = new DirectoriesConfig(root, ["data", "templates"]);
        var shipped = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        _templates = new(new StubDataLoaderService().With(shipped));
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(shipped)).LoadDataAsync()).Entities;
        var catalog = new SpellCatalogService(new StubDataLoaderService().With(spells.ToArray()), _templates);

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
                        Map = MapType.Trammel, Name = "Sealed", TeleportIn = false, RecallIn = false,
                        Areas = [new RegionAreaContent { X1 = 40, Y1 = 40, X2 = 60, Y2 = 60 }]
                    },
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Town", Guarded = true,
                        Areas = [new RegionAreaContent { X1 = 18, Y1 = 0, X2 = 24, Y2 = 12 }]
                    },
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Shut", TeleportOut = false, RecallOut = false,
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
        var spellScripts = new SpellScriptService(_engine, _loop, options);
        await spellScripts.StartAsync();
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
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    [Theory]
    [InlineData("agility", StatBonusType.Dexterity, 6)]
    [InlineData("cunning", StatBonusType.Intelligence, 6)]
    [InlineData("strength", StatBonusType.Strength, 6)]
    public void ABuff_RaisesTheStatByOneAndATenthOfMagery_ForOneAndTwoTenthsOfASecondAPoint(
        string key,
        StatBonusType stat,
        int mana
    )
    {
        Cast(key);

        Assert.Empty(_errors);
        Assert.Equal(11, _bonuses.Bonus(_bran, stat));
        Assert.Equal(30 - mana, _aria.Mana);
        Assert.Equal(TimeSpan.FromSeconds(120), _timers.Timers.Single(timer => timer.Name == "stat_bonus").Interval);
        Assert.Contains(_effects.On, shown => shown.Target == _bran.Id);
    }

    [Fact]
    public void Bless_RaisesTheThreeStatsAtOnce()
    {
        Cast("bless");

        Assert.Empty(_errors);
        Assert.Equal(
            (11, 11, 11),
            (
                _bonuses.Bonus(_bran, StatBonusType.Strength),
                _bonuses.Bonus(_bran, StatBonusType.Dexterity),
                _bonuses.Bonus(_bran, StatBonusType.Intelligence)
            )
        );
        Assert.Equal(21, _aria.Mana);
    }

    [Fact]
    public void ABuff_ReplacesAWeakerOne_AndAStrongerOneStays()
    {
        _bonuses.TryAddBuff(_bran, StatBonusType.Dexterity, 5, TimeSpan.FromMinutes(1));
        Cast("agility");
        Assert.Equal(11, _bonuses.Bonus(_bran, StatBonusType.Dexterity));

        _time.Advance(TimeSpan.FromSeconds(2));
        _bonuses.TryAddBuff(_bran, StatBonusType.Strength, 20, TimeSpan.FromMinutes(1));
        Cast("strength");

        Assert.Equal(20, _bonuses.Bonus(_bran, StatBonusType.Strength));
        Assert.Empty(_errors);
    }

    [Fact]
    public void ABuff_OnADeadTarget_IsRefusedBeforeAnythingIsSpent()
    {
        _bran.Body = 0x0192;

        Cast("bless");

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
        Assert.Equal(0, _bonuses.Bonus(_bran, StatBonusType.Strength));
    }

    [Fact]
    public void Cure_EndsAPoisonThatItsChanceCovers_AndTellsBoth()
    {
        _container.Resolve<IPoisonService>().Apply(_bran, 1);
        Roll(pick: 99);

        Cast("cure");

        // (10000 + 7500 - 2 x 1750) / 100 = 140 per cent.
        Assert.Empty(_errors);
        Assert.Null(_container.Resolve<IPoisonService>().LevelOf(_bran));
        Assert.Contains(1010058, ToldTo(_aria));
        Assert.Contains(1010059, ToldTo(_bran));
        Assert.Equal(24, _aria.Mana);
    }

    [Fact]
    public void Cure_ThatMissesItsChance_LeavesThePoisonAndTellsTheCaster()
    {
        _container.Resolve<IPoisonService>().Apply(_bran, 4);
        Roll(pick: 99);

        Cast("cure");

        // (10000 + 7500 - 5 x 1750) / 100 = 87 per cent, and the roll is 99.
        Assert.Equal(4, _container.Resolve<IPoisonService>().LevelOf(_bran));
        Assert.Contains(1010060, ToldTo(_aria));
    }

    [Theory]
    [InlineData(11, 8)]
    [InlineData(12, 4)]
    [InlineData(20, 2)]
    public void Harm_HurtsAtOnce_WholeNextToTheCaster_HalvedAtTwoTiles_AndAQuarterBeyond(int x, int damage)
    {
        _bran.Location = new Point3D(x, 10, 0);
        Roll(pick: 8);

        Cast("harm");

        // 8 x 1.1 = 8.8; 4 x 1.1 = 4.4; 2 x 1.1 = 2.2.
        Assert.Empty(_errors);
        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _bran, damage), (harm.Attacker, harm.Target, harm.Damage));
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.DoesNotContain(_timers.Timers, timer => Math.Abs(timer.Interval.TotalSeconds - 0.5) < 0.001);
        Assert.Equal(24, _aria.Mana);
    }

    [Fact]
    public void Harm_ThatTheTargetResists_DoesThreeQuartersOfIt_AndTheTargetIsTold()
    {
        _bran.Location = new Point3D(11, 10, 0);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 1000 });
        Roll(pick: 8, roll: 0);

        Cast("harm");

        // 8 x 0.75 x (1 + (50 - 100) / 200) = 4.5.
        Assert.Equal(4, Assert.Single(_combat.Harmed).Damage);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void Protection_RaisesTheArmorForAWhile_AndASecondOneIsRefusedBeforeAnythingIsSpent()
    {
        Cast("protection");

        Assert.Empty(_errors);
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        Assert.Equal((10L, now + 120), (_bran.GetProp<long>("magic.armor"), _bran.GetProp<long>("magic.armor_until")));
        Assert.Equal(24, _aria.Mana);

        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("protection");

        Assert.Contains(1005559, ToldTo(_aria));
        Assert.Equal(24, _aria.Mana);
    }

    [Fact]
    public void ReactiveArmor_StartsAReflectionForAWhile_AndASecondOneIsRefused()
    {
        Cast("reactive_armor");

        Assert.Empty(_errors);
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        // 25 + 100 / 2 seconds; the percent is read from the Magery of the wearer when it is hit.
        Assert.Equal(now + 75, _bran.GetProp<long>("magic.reactive_until"));
        Assert.False(_bran.TryGetProp<long>("magic.reactive_percent", out _));
        Assert.Equal(26, _aria.Mana);

        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("reactive_armor");

        Assert.Contains(1005559, ToldTo(_aria));
        Assert.Equal(26, _aria.Mana);
    }

    [Fact]
    public void Fireball_FliesToTheTarget_AndHurtsItHalfASecondLater()
    {
        Roll(pick: 12);
        Cast("fireball");

        Assert.Empty(_errors);
        Assert.Equal(21, _aria.Mana);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Contains(_effects.Moving, moving => moving.Source == _aria.Id && moving.Target == _bran.Id && moving.Options.Graphic == 0x36D4);
        Assert.Empty(_combat.Harmed);

        FireHalfSecond();

        // 12 x 1.1 = 13.2.
        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _bran, 13), (harm.Attacker, harm.Target, harm.Damage));
    }

    [Theory]
    [InlineData(0, 1.0, 0)]
    [InlineData(50, 1.0, 1)]
    [InlineData(80, 1.0, 2)]
    [InlineData(100, 1.0, 2)]
    [InlineData(100, 0.0, 3)]
    public void Poison_IsAsStrongAsMageryAndPoisoningTogether_AndTheDeadlyOneIsOneInTen(
        int poisoning,
        double roll,
        int level
    )
    {
        // Magery is 100 points: next to the caster, the levels go by the sum alone.
        _bran.Location = new Point3D(11, 10, 0);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Poisoning, Base = poisoning * 10 });
        Roll(pick: 0, roll: roll);

        Cast("poison");

        Assert.Empty(_errors);
        Assert.Equal(level, _container.Resolve<IPoisonService>().LevelOf(_bran));
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Equal(21, _aria.Mana);
    }

    [Fact]
    public void Poison_FartherThanThreeTiles_LosesTenPointsATile()
    {
        // 100 + 100 less 10 for each tile beyond three (six away: 30) is 170: the regular one, not the greater.
        _bran.Location = new Point3D(16, 10, 0);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Poisoning, Base = 1000 });
        Roll(pick: 0, roll: 1);

        Cast("poison");

        Assert.Equal(1, _container.Resolve<IPoisonService>().LevelOf(_bran));
    }

    [Fact]
    public void Poison_ThatTheTargetResists_PoisonsNoOne_AndTheTargetIsTold()
    {
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 1000 });
        Roll(pick: 0, roll: 0);

        Cast("poison");

        Assert.Null(_container.Resolve<IPoisonService>().LevelOf(_bran));
        Assert.Contains(501783, ToldTo(_bran));
        Assert.Equal(21, _aria.Mana);
    }

    [Fact]
    public void Lightning_StrikesAtOnce_ForTwelveToTwenty()
    {
        Roll(pick: 15);

        Cast("lightning");

        // 15 x 1.1 = 16.5.
        Assert.Empty(_errors);
        Assert.Contains(_effects.Lightning, bolt => bolt.Target == _bran.Id);
        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _bran, 16), (harm.Attacker, harm.Target, harm.Damage));
        Assert.Equal(19, _aria.Mana);
        Assert.Contains((_bran, 0x29), _speech.Sounds);
    }

    [Fact]
    public void ManaDrain_TakesOneToAHundredMana_OfATargetThatDoesNotResist()
    {
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 0 });
        Roll(pick: 17, roll: 1);
        // The roll of 1 is above the 99 per cent: the drain gets through.
        _bran.Mana = 40;

        Cast("mana_drain");

        // The chance is 99 per cent, and the roll 1.0 is above it.
        Assert.Empty(_errors);
        Assert.Equal(23, _bran.Mana);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
    }

    [Fact]
    public void ManaDrain_IsResistedNinetyNineTimesInAHundred()
    {
        _bran.Mana = 40;
        Roll(pick: 17, roll: 0.5);

        Cast("mana_drain");

        Assert.Equal(40, _bran.Mana);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void GreaterHeal_GivesBackFourTenthsOfMagery_AndOneToTen()
    {
        _bran.HitsMax = 100;
        Roll(pick: 7);

        Cast("greater_heal");

        // 100 x 0.4 + 7 on top of 20.
        Assert.Empty(_errors);
        Assert.Equal(67, _bran.Hits);
        Assert.Equal(19, _aria.Mana);
    }

    [Fact]
    public void GreaterHeal_IsRefusedForThePoisoned_AsTheClientSaysIt()
    {
        _container.Resolve<IPoisonService>().Apply(_bran, 1);

        Cast("greater_heal");

        Assert.Contains(1010398, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void Curse_LowersTheThreeStatsAtOnce_AndDisturbsTheTarget()
    {
        Cast("curse");

        Assert.Empty(_errors);
        Assert.Equal(
            (-11, -11, -11),
            (
                _bonuses.Bonus(_bran, StatBonusType.Strength),
                _bonuses.Bonus(_bran, StatBonusType.Dexterity),
                _bonuses.Bonus(_bran, StatBonusType.Intelligence)
            )
        );
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Equal(19, _aria.Mana);
    }

    [Fact]
    public void Curse_OnAnInvulnerable_SpendsNothing()
    {
        _bran.Notoriety = NotorietyType.Invulnerable;

        Cast("curse");

        Assert.Contains(1001018, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void ArchCure_CuresEveryoneAroundThePlace_WhoseChanceCovers()
    {
        var poison = _container.Resolve<IPoisonService>();
        var orc = Npc(0x200, OrcBody);
        poison.Apply(_bran, 0);
        poison.Apply(orc, 1);
        Roll(pick: 50);

        CastAt("arch_cure", new Point3D(12, 10, 0));

        // (10000 + 7500 - 2 x 1750) / 100 - 1 = 139 for the lesser one, 139 - 17 = 122 for the regular: both over 50.
        Assert.Empty(_errors);
        Assert.Null(poison.LevelOf(_bran));
        Assert.Null(poison.LevelOf(orc));
        Assert.Contains(1010058, ToldTo(_aria));
        Assert.Equal(19, _aria.Mana);
        Assert.Contains(_speech.PlacedSounds, played => played.Sound == 0x299);
    }

    [Fact]
    public void ArchCure_LeavesTheDeadAndThoseOutOfRange()
    {
        var poison = _container.Resolve<IPoisonService>();
        var far = Npc(0x200, OrcBody);
        far.Location = new Point3D(30, 10, 0);
        poison.Apply(far, 0);
        Roll(pick: 0);

        CastAt("arch_cure", new Point3D(12, 10, 0));

        Assert.Equal(0, poison.LevelOf(far));
    }

    [Fact]
    public void ArchProtection_ProtectsEveryoneAliveWithinThreeTiles_AndNoOneTwice()
    {
        var near = Npc(0x200, OrcBody);
        var far = Npc(0x201, OrcBody);
        far.Location = new Point3D(30, 10, 0);
        _bran.SetProp("magic.armor", 5L);
        _bran.SetProp("magic.armor_until", _time.GetUtcNow().ToUnixTimeSeconds() + 500);

        CastAt("arch_protection", new Point3D(12, 10, 0));

        Assert.Empty(_errors);
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        Assert.Equal((10L, now + 120), (near.GetProp<long>("magic.armor"), near.GetProp<long>("magic.armor_until")));
        Assert.Equal(0L, far.GetProp("magic.armor", 0L));
        // The one that had it keeps its own.
        Assert.Equal(5L, _bran.GetProp<long>("magic.armor"));
        Assert.Equal(19, _aria.Mana);
    }

    [Fact]
    public void Teleport_PutsTheCasterAtThePlace_WithAPuffAtBothEnds()
    {
        _movement.SpawnZ = (_, _) => 5;

        CastAt("teleport", new Point3D(14, 10, 0));

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(14, 10, 5), _aria.Location);
        Assert.Equal(21, _aria.Mana);
        Assert.Equal(2, _effects.At.Count(shown => shown.Options.Graphic == 0x372A));
        Assert.Equal(2, _speech.PlacedSounds.Count(played => played.Sound == 0x1FE));
    }

    [Fact]
    public void Teleport_ToAPlaceNoOneCanStandOn_IsRefusedBeforeAnythingIsSpent()
    {
        _movement.SpawnZ = (_, _) => null;

        CastAt("teleport", new Point3D(14, 10, 0));

        Assert.Equal(new Point3D(10, 10, 0), _aria.Location);
        Assert.Contains(501942, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void Teleport_ToAPlaceWhereAMobileStandsOrADoorIsShut_IsRefusedBeforeAnythingIsSpent()
    {
        _movement.SpawnZ = (_, _) => 0;
        Place(_bran, new Point3D(14, 10, 0));
        Ground("door", 0x0692, new Point3D(14, 12, 0));

        CastAt("teleport", new Point3D(14, 10, 0));
        _time.Advance(TimeSpan.FromSeconds(2));
        CastAt("teleport", new Point3D(14, 12, 0));

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(10, 10, 0), _aria.Location);
        Assert.Equal(2, ToldTo(_aria).Count(told => told == 501942));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void Recall_ToAPlaceWhereAMobileStands_IsRefusedBeforeAnythingIsSpent()
    {
        _movement.SpawnZ = (_, _) => 0;
        var rune = Rune(30, 30, 0, marked: true);
        Place(_bran, new Point3D(30, 30, 0));

        Cast("recall", rune.Id);

        Assert.Empty(_errors);
        Assert.Contains(501942, ToldTo(_aria));
        Assert.Equal(new Point3D(10, 10, 0), _aria.Location);
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void Teleport_IntoARegionThatForbidsIt_OrOutOfOne_IsRefused()
    {
        _movement.SpawnZ = (_, _) => 0;
        _aria.Location = new Point3D(42, 42, 0);
        _bran.Location = new Point3D(44, 42, 0);

        CastAt("teleport", new Point3D(46, 42, 0));
        Assert.Equal(new Point3D(42, 42, 0), _aria.Location);
        Assert.Equal(30, _aria.Mana);

        _time.Advance(TimeSpan.FromSeconds(2));
        _aria.Location = new Point3D(10, 42, 0);
        CastAt("teleport", new Point3D(13, 42, 0));
        Assert.Equal(new Point3D(10, 42, 0), _aria.Location);
        Assert.Contains(502361, ToldTo(_aria));
        Assert.Contains(501035, ToldTo(_aria));
    }

    [Fact]
    public void Telekinesis_UsesAnItemFromAfar_ForTheCaster()
    {
        var chest = Ground("chest", 0x0E40, new Point3D(15, 12, 0));

        Cast("telekinesis", chest.Id);

        Assert.Empty(_errors);
        Assert.Equal(21, _aria.Mana);
        Assert.Equal(chest.Id, Assert.Single(_uses.UsedFromAfar).Target);
        Assert.Contains(_effects.At, shown => shown.Options.Graphic == 0x376A);
    }

    [Fact]
    public void Telekinesis_OnAnItemThatCannotBeUsedFromAfar_IsRefusedBeforeAnythingIsSpent()
    {
        var stone = Ground("0x0e40_stone", 0x1363, new Point3D(15, 12, 0));
        _uses.CanUse = false;

        Cast("telekinesis", stone.Id);

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
        Assert.Empty(_uses.UsedFromAfar);
    }

    [Fact]
    public void WallOfStone_RaisesThreePiecesAcrossTheWay_ThatGoAwayAfterTenSeconds()
    {
        _movement.SpawnZ = (_, _) => 0;

        CastAt("wall_of_stone", new Point3D(14, 10, 0));

        Assert.Empty(_errors);
        var pieces = PiecesNear(14, 10, "magic_wall_of_stone");
        Assert.Equal([(14, 9), (14, 10), (14, 11)], pieces.Select(piece => (piece.GroundLocation!.Value.X, piece.GroundLocation!.Value.Y)).OrderBy(spot => spot.Item2));
        Assert.Equal(21, _aria.Mana);
        Assert.All(pieces, piece => Assert.Equal(TimeSpan.FromSeconds(10), _itemTimers.Remaining(piece, "expire")));

        RunItemTimers(TimeSpan.FromSeconds(11));

        Assert.Empty(PiecesNear(14, 10, "magic_wall_of_stone"));
    }

    [Fact]
    public void WallOfStone_SkipsAPlaceWhereSomeoneStands()
    {
        _movement.SpawnZ = (_, _) => 0;
        Place(_bran, new Point3D(14, 10, 0));

        CastAt("wall_of_stone", new Point3D(14, 10, 0));

        Assert.Equal(2, PiecesNear(14, 10, "magic_wall_of_stone").Count);
    }

    [Theory]
    [InlineData("wall_of_stone", "magic_wall_of_stone")]
    [InlineData("fire_field", "magic_fire_field_ew")]
    public void AField_AimedAtAGuardedTown_IsRefusedBeforeAnythingIsSpent(string key, string template)
    {
        _movement.SpawnZ = (_, _) => 0;

        CastAt(key, new Point3D(20, 10, 0));

        Assert.Empty(_errors);
        Assert.Contains(500946, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
        Assert.Empty(PiecesNear(20, 10, template));
        Assert.Empty(PiecesNear(20, 10, "magic_fire_field_ns"));
        Assert.Empty(PiecesNear(20, 10, "magic_wall_of_stone"));
    }

    [Fact]
    public void FireField_RaisesFivePieces_AlongTheLineAcrossTheWay_ForTwentySeconds()
    {
        _movement.SpawnZ = (_, _) => 0;

        // From (10, 10) to (14, 14) the line runs from east to west.
        CastAt("fire_field", new Point3D(14, 14, 0));

        Assert.Empty(_errors);
        var pieces = PiecesNear(14, 14, "magic_fire_field_ew");
        Assert.Equal(5, pieces.Count);
        Assert.Equal([12, 13, 14, 15, 16], pieces.Select(piece => piece.GroundLocation!.Value.X).Order());
        Assert.Equal(19, _aria.Mana);

        RunItemTimers(TimeSpan.FromSeconds(21));

        Assert.Empty(PiecesNear(14, 14, "magic_fire_field_ew"));
    }

    [Fact]
    public void FireField_BurnsWhoStandsInIt_OncePerSecond_AndTheCasterIsTheAggressor()
    {
        _movement.SpawnZ = (_, _) => 0;
        Place(_bran, new Point3D(14, 14, 0));
        _skills.ResultBySkill = skill => skill != SkillType.ResistingSpells;
        CastAt("fire_field", new Point3D(14, 14, 0));

        // A second later the piece under bran wakes and burns it for 2.
        RunItemTimers(TimeSpan.FromSeconds(1));

        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _bran, 2), (harm.Attacker, harm.Target, harm.Damage));
        Assert.Contains((_bran, 0x208), _speech.Sounds);
    }

    [Fact]
    public void FireField_BurnsWhoStepsOntoIt_ButNotTwiceInTheSameSecond()
    {
        _movement.SpawnZ = (_, _) => 0;
        _skills.ResultBySkill = skill => skill != SkillType.ResistingSpells;
        CastAt("fire_field", new Point3D(14, 14, 0));
        var piece = PiecesNear(14, 14, "magic_fire_field_ew").Single(found => found.GroundLocation!.Value.X == 14);
        Place(_bran, new Point3D(14, 14, 0));

        _itemScripts.Run(piece, "on_move_over", (long)_bran.Id.Value);
        _itemScripts.Run(piece, "on_move_over", (long)_bran.Id.Value);

        Assert.Single(_combat.Harmed);
    }

    [Fact]
    public void FireField_ThatTheBurnedResists_BurnsForOne()
    {
        _movement.SpawnZ = (_, _) => 0;
        Place(_bran, new Point3D(14, 14, 0));
        CastAt("fire_field", new Point3D(14, 14, 0));

        RunItemTimers(TimeSpan.FromSeconds(1));

        Assert.Equal(1, Assert.Single(_combat.Harmed).Damage);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void FireField_LeavesAnInvulnerableAlone()
    {
        _movement.SpawnZ = (_, _) => 0;
        Place(_bran, new Point3D(14, 14, 0));
        _combat.Allows = false;
        CastAt("fire_field", new Point3D(14, 14, 0));

        RunItemTimers(TimeSpan.FromSeconds(1));

        Assert.Empty(_errors);
        Assert.DoesNotContain(_combat.Harmed, harm => harm.Attacker is null);
    }

    [Fact]
    public void Recall_CarriesTheCasterToTheMarkedPlaceOfARune_WithTheSoundAtBothEnds()
    {
        _movement.SpawnZ = (_, _) => 20;
        var rune = Rune(30, 30, 20, marked: true);

        Cast("recall", rune.Id);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(30, 30, 20), _aria.Location);
        Assert.Equal(19, _aria.Mana);
        Assert.Equal(2, _speech.Sounds.Count(played => played is { Source: var who, Sound: 0x1FC } && who == _aria));
    }

    [Fact]
    public void Recall_OfAnUnmarkedRune_OrAnotherItem_IsRefusedBeforeAnythingIsSpent()
    {
        var blank = Rune(0, 0, 0, marked: false);
        var other = Carry("0x0e40_stone", 0x1363, 1);

        Cast("recall", blank.Id);
        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("recall", other.Id);

        Assert.Contains(501805, ToldTo(_aria));
        Assert.Contains(502357, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
        Assert.Equal(new Point3D(10, 10, 0), _aria.Location);
    }

    [Fact]
    public void Recall_IsRefusedForACriminal_ARuneOfAnotherMap_AndAPlaceTheRegionForbids()
    {
        _movement.SpawnZ = (_, _) => 0;
        var elsewhere = Rune(30, 30, 0, marked: true);
        elsewhere.SetProp("rune.map", (long)MapType.Felucca);
        var sealedPlace = Rune(45, 45, 0, marked: true);

        Cast("recall", elsewhere.Id);
        Assert.Contains(1005569, ToldTo(_aria));

        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("recall", sealedPlace.Id);
        Assert.Contains(1019004, ToldTo(_aria));

        Assert.Equal(30, _aria.Mana);
        Assert.Equal(new Point3D(10, 10, 0), _aria.Location);
    }

    [Theory]
    [InlineData("magic_trap")]
    [InlineData("magic_untrap")]
    [InlineData("magic_lock")]
    [InlineData("unlock")]
    public void TheSpellsOfLocksAndTraps_StayDisabled_BecauseContainersHaveNeither(string key)
    {
        Assert.True(_casts.CastFromBook(_aria, SpellId(key)) is false);
        Assert.Equal(30, _aria.Mana);
        Assert.Contains(502345, ToldTo(_aria));
    }

    [Fact]
    public void EverySpellOfTheThreeCircles_HasAScript_ButTheFourOfLocksAndTraps()
    {
        var catalog = _container.Resolve<ISpellCatalogService>();
        var disabled = new[] { "magic_trap", "magic_untrap", "magic_lock", "unlock" };

        foreach (var id in Enumerable.Range(9, 24).Append(7))
        {
            Assert.True(catalog.TryGet(id, out var found));
            var built = File.Exists(Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "spells", found.Key + ".lua"));
            Assert.Equal(!disabled.Contains(found.Key), built);
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

    private void WriteScripts(string root)
    {
        var scripts = Path.Combine(root, "scripts");
        _scripts.Write(
            "common/magic.lua",
            File.ReadAllText(Path.Combine(scripts, "common", "magic.lua"))
                .Replace("magic.random = math.random", "magic.random = function() return ROLL end")
        );

        var keys = new[]
        {
            "clumsy", "create_food", "feeblemind", "heal", "magic_arrow", "night_sight", "weaken", "reactive_armor",
            "agility", "cunning", "cure", "harm", "protection", "strength", "bless", "fireball", "poison", "teleport",
            "telekinesis", "wall_of_stone", "arch_cure", "arch_protection", "curse", "fire_field", "greater_heal",
            "lightning", "mana_drain", "recall"
        };

        foreach (var key in keys)
        {
            var text = File.ReadAllText(Path.Combine(scripts, "spells", key + ".lua"));
            var hook = text.Contains(key + ".random = math.random", StringComparison.Ordinal)
                ? $"\n{key}.random = function(low, high) return math.min(math.max(PICK, low), high) end\n"
                : "";
            _scripts.Write($"spells/{key}.lua", text + hook);
        }

        foreach (var name in new[] { "spellbook", "spell_scroll", "magic_field" })
        {
            _scripts.Write($"items/{name}.lua", File.ReadAllText(Path.Combine(scripts, "items", name + ".lua")));
        }

        _scripts.Write("common/field.lua", File.ReadAllText(Path.Combine(scripts, "common", "field.lua")));

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
    }

    private void CastAt(string key, Point3D place)
    {
        Assert.True(_casts.CastFromBook(_aria, SpellId(key)), key);
        FireDelay();

        if (_targets.Waiting)
        {
            _targets.Answer(TargetResult.ForLocation(_aria.Map, place));
        }
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

    private void CastNoTarget(string key)
    {
        Assert.True(_casts.CastFromBook(_aria, SpellId(key)), key);
        FireDelay();
    }

    private int SpellId(string key)
    {
        return _container.Resolve<ISpellCatalogService>().TryGetByKey(key, out var spell) ? spell.Id : 0;
    }

    private void FireDelay()
    {
        var delay = _timers.Timers.FirstOrDefault(timer => timer.Name == "spell_cast");

        if (delay is not null)
        {
            _loop.DeferTryPost = true;
            _timers.Fire(delay.Id);

            while (_loop.Deferred.Count > 0)
            {
                _loop.RunDeferred();
            }

            _loop.DeferTryPost = false;
        }
    }

    private void FireHalfSecond()
    {
        _timers.Fire(_timers.Timers.Single(timer => Math.Abs(timer.Interval.TotalSeconds - 0.5) < 0.001 && timer.Name != "spell_cast").Id);
    }

    private List<int> ToldTo(MobileEntity player)
    {
        return _speech.ToldClilocs.Where(told => told.Player == player).Select(told => told.Cliloc).ToList();
    }

    private MobileEntity Npc(uint serial, int body)
    {
        var npc = new MobileEntity
        {
            Id = new Serial(serial), Name = "an orc", TemplateId = "orc", Body = body, Map = _aria.Map,
            Location = new Point3D(12, 11, 0), Hits = 30, HitsMax = 30
        };
        _fixture.Mobiles.EnterWorld(npc);

        return npc;
    }

    private ItemEntity Carry(string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([item]);

        return item;
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
