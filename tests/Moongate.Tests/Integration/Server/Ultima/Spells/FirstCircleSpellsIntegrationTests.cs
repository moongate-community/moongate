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
///     The shipped first circle: the scripts of <c>scripts/spells</c> and <c>scripts/common/magic.lua</c> over the real cast
///     and spellbook services and the shipped spells and item templates, from the cast to the effect.
/// </summary>
public sealed class FirstCircleSpellsIntegrationTests : IAsyncLifetime
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
    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
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
        _container.RegisterInstance<ITileDataService>(new FakeTileDataService().Item(0x0E75, TileFlagType.Container, 0));
        _container.RegisterInstance<IContainerCapacityService>(_capacity);
        _container.RegisterInstance<IInventoryMutationGuard>(_guard);
        _container.RegisterInstance<TimeProvider>(_time);
        _container.RegisterInstance<IMapService>(_map);
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
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

    [Fact]
    public void MagicArrow_FliesToTheTarget_AndHurtsItHalfASecondLater()
    {
        Roll(pick: 7);
        Cast("magic_arrow");

        Assert.Empty(_errors);
        Assert.Equal(26, _aria.Mana);
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Contains(_effects.Moving, moving => moving.Source == _aria.Id && moving.Target == _bran.Id && moving.Options.Graphic == 0x36E4);
        Assert.Empty(_combat.Harmed);

        _timers.Fire(_timers.Timers.Single(timer => Math.Abs(timer.Interval.TotalSeconds - 0.5) < 0.001).Id);

        // 7, scaled by 1 + (50 - 0) / 500 = 1.1 and by 1 + (100 - 100) / 400.
        var harm = Assert.Single(_combat.Harmed);
        Assert.Equal((_aria, _bran, 7), (harm.Attacker, harm.Target, harm.Damage));
        Assert.Equal(26, _aria.Mana);
    }

    [Fact]
    public void MagicArrow_ThatTheTargetResists_DoesThreeQuartersOfIt_AndTheTargetIsTold()
    {
        _state.Skills.Add(new MobileSkill { Skill = SkillType.ResistingSpells, Base = 1000 });
        Roll(pick: 4, roll: 0);
        Cast("magic_arrow");

        FireHalfSecond();

        // 4 x 0.75 x (1 + (50 - 100) / 200) = 2.25.
        Assert.Equal(2, Assert.Single(_combat.Harmed).Damage);
        Assert.Contains(501783, ToldTo(_bran));
    }

    [Fact]
    public void MagicArrow_AgainstAMonster_DoesDouble()
    {
        var orc = Npc(0x200, OrcBody);
        Roll(pick: 4);
        Cast("magic_arrow", orc.Id);

        FireHalfSecond();

        // 4 x 1.1 x 2 = 8.8.
        Assert.Equal((orc, 8), (Assert.Single(_combat.Harmed).Target, Assert.Single(_combat.Harmed).Damage));
    }

    [Fact]
    public void MagicArrow_AtADeadTarget_IsRefusedBeforeAnythingIsSpent()
    {
        _bran.Body = 0x0192;

        Cast("magic_arrow");

        Assert.Equal(30, _aria.Mana);
        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Empty(_combat.Aggressed);
    }

    [Fact]
    public void Heal_GivesBackATenthOfMagery_AndOneToFive()
    {
        Roll(pick: 3);

        Cast("heal");

        Assert.Empty(_errors);
        // 100 x 0.1 + 3.
        Assert.Equal(33, _bran.Hits);
        Assert.Equal(26, _aria.Mana);
        Assert.Contains(_effects.On, effect => effect.Target == _bran.Id && effect.Options.Graphic == 0x376A);
    }

    [Fact]
    public void Heal_StopsAtTheMaximum_AndIsRefusedAtFullHits()
    {
        _bran.Hits = 48;
        Roll(pick: 5);

        Cast("heal");

        Assert.Equal(50, _bran.Hits);

        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("heal");

        Assert.Contains(WontWork, ToldTo(_aria));
        Assert.Equal(26, _aria.Mana);
    }

    [Fact]
    public void Heal_ThePoisonedAreRefused_AsTheClientSaysIt()
    {
        _container.Resolve<IPoisonService>().Apply(_bran, 1);

        Cast("heal");

        Assert.Contains(1010398, ToldTo(_aria));
        Assert.Equal(20, _bran.Hits);
        Assert.Equal(30, _aria.Mana);
    }

    [Theory]
    [InlineData("clumsy", StatBonusType.Dexterity)]
    [InlineData("feeblemind", StatBonusType.Intelligence)]
    [InlineData("weaken", StatBonusType.Strength)]
    public void ACurse_LowersTheStatOfTheTarget_ForAMinuteAndAHalf_AndMakesTheCasterItsAggressor(
        string key, StatBonusType stat
    )
    {
        Cast(key);

        Assert.Empty(_errors);
        // 1 + 100 x 0.1 points, for 100 x 1.2 seconds.
        Assert.Equal(-11, _bonuses.Bonus(_bran, stat));
        Assert.Contains(_timers.Timers, timer => timer.Interval == TimeSpan.FromSeconds(120));
        Assert.Equal([(_aria, _bran)], _combat.Aggressed);
        Assert.Equal(26, _aria.Mana);
    }

    [Theory]
    [InlineData("clumsy")]
    [InlineData("feeblemind")]
    [InlineData("weaken")]
    [InlineData("magic_arrow")]
    public void AHarmfulSpell_OnAnInvulnerableTarget_SpendsNothingAndChangesNothing(string key)
    {
        _bran.Notoriety = NotorietyType.Invulnerable;

        Cast(key);

        Assert.Empty(_errors);
        Assert.Contains(1001018, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
        Assert.Equal((0, 0, 0), (_bonuses.Bonus(_bran, StatBonusType.Strength), _bonuses.Bonus(_bran, StatBonusType.Dexterity), _bonuses.Bonus(_bran, StatBonusType.Intelligence)));
    }

    [Theory]
    [InlineData("clumsy", StatBonusType.Dexterity)]
    [InlineData("feeblemind", StatBonusType.Intelligence)]
    [InlineData("weaken", StatBonusType.Strength)]
    public void ACurse_TheCombatRefusesToAggress_LowersNothing(string key, StatBonusType stat)
    {
        _combat.Allows = false;

        Cast(key);

        Assert.Empty(_errors);
        Assert.Equal(0, _bonuses.Bonus(_bran, stat));
        Assert.DoesNotContain(_timers.Timers, timer => timer.Name == "stat_curse");
    }

    [Fact]
    public void Weaken_LowersTheMaximumHits_AndTakesWhatIsAboveIt()
    {
        _bran.Hits = 50;

        Cast("weaken");

        Assert.Equal((39, 39), (_bran.EffectiveHitsMax, _bran.Hits));
    }

    [Fact]
    public void ACurseNoStrongerThanTheOneThere_ChangesNothing()
    {
        Cast("clumsy");
        _time.Advance(TimeSpan.FromSeconds(2));
        _state.Skills.Single(skill => skill.Skill == SkillType.Magery).Base = 500;

        Cast("clumsy");

        Assert.Equal(-11, _bran.DexterityBonus);
        Assert.Single(_timers.Timers, timer => timer.Name == "stat_curse");
    }

    [Fact]
    public void NightSight_LightsTheTarget_AtALevelOfTheMagery_AndTheSecondIsTold()
    {
        Roll(pick: 20);

        Cast("night_sight");

        Assert.Empty(_errors);
        Assert.True(_bonuses.HasNightSight(_bran));
        Assert.Contains(_timers.Timers, timer => timer.Interval == TimeSpan.FromMinutes(20));
        Assert.Contains(_fixture.Sender.Sent.OfType<PersonalLightLevelPacket>(), packet => packet.Level == 26);

        _time.Advance(TimeSpan.FromSeconds(2));
        Cast("night_sight");

        Assert.Contains("They already have nightsight.", _speech.Told.Select(told => told.Text));
    }

    [Fact]
    public void CreateFood_PutsAFoodInTheBackpack_AndTellsWhich()
    {
        Roll(pick: 1);

        CastNoTarget("create_food");

        Assert.Empty(_errors);
        var food = Assert.Single(_items.GetContents(_backpack.Id), item => item.TemplateId == "0x09d1_grape_bunch");
        Assert.Equal(_backpack.Id, food.ContainerId);
        Assert.Contains(_speech.Told, told => told.Text == "You magically create food in your backpack: grapes");
        Assert.Equal(26, _aria.Mana);
    }

    [Fact]
    public void AScroll_CastsWithoutReagents_ByADoubleClick_AndIsUsedUp()
    {
        var scroll = Carry("0x1f31_heal_scroll", 0x1F31, 2);
        Roll(pick: 1);
        _itemScripts.Run(scroll, "on_use", Aria);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Empty(_errors);
        Assert.Equal(31, _bran.Hits);
        Assert.Equal(1, scroll.Amount);
        Assert.Equal(26, _aria.Mana);
    }

    [Fact]
    public void TheBook_OpensOnADoubleClick_WhenCarried()
    {
        _itemScripts.Run(_book, "on_use", Aria);

        Assert.Empty(_errors);
        var content = Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Equal(64, content.Items.Count);
    }

    [Fact]
    public void TheBook_OnTheGround_IsToldItMustBeCarried()
    {
        var far = new ItemEntity { Id = new Serial(_next++), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _items.Add([far]);
        _items.PlaceOnGround(far, _aria.Map, new Point3D(11, 10, 0));

        _itemScripts.Run(far, "on_use", Aria);

        Assert.Empty(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Contains(500207, ToldTo(_aria));
    }

    [Fact]
    public void ASpellWithoutAScript_IsTheDisabledOne()
    {
        Assert.False(_casts.CastFromBook(_aria, SpellId("reactive_armor")));

        Assert.Contains(502345, ToldTo(_aria));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void TheSpellModule_AddsAndListsTheSpellsOfABook_AndTellsWhatASpellIs()
    {
        var empty = Carry("spellbook", 0x0EFA, 1);
        var scroll = Carry("0x1f31_heal_scroll", 0x1F31, 1);
        _scripts.Write(
            "test/api.lua",
            """
            api = {}
            function api.run(book, scroll)
                local before = spell.has(book, "heal")
                local added = spell.add(book, 4)
                local again = spell.add(book, "heal")
                local list = spell.spells(book)
                local info = spell.info("magic_arrow")
                return before, added, again, #list, list[1], info.circle, info.mana, info.target, info.harmful,
                    spell.of_scroll(scroll), spell.info(99) == nil
            end
            """
        );
        _engine.LoadFile("test/api.lua");

        var result = _engine.CallMember("test/api.lua", "api", "run", (long)empty.Id.Value, (long)scroll.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal(
            [false, true, false, 1.0, 4.0, 1.0, 4.0, "mobile", true, 4.0, true],
            result.Values
        );
        Assert.True(_books.Has(empty, 4));
    }

    [Fact]
    public void TheSpellModule_CastsFromTheBook_AndCancels()
    {
        _scripts.Write(
            "test/cast.lua",
            """
            cast = {}
            function cast.start(user) return spell.cast(user, "heal") end
            function cast.state(user) return spell.is_casting(user) end
            function cast.stop(user) return spell.cancel(user) end
            """
        );
        _engine.LoadFile("test/cast.lua");

        var started = _engine.CallMember("test/cast.lua", "cast", "start", (long)Aria);
        var casting = _engine.CallMember("test/cast.lua", "cast", "state", (long)Aria);
        var stopped = _engine.CallMember("test/cast.lua", "cast", "stop", (long)Aria);
        var after = _engine.CallMember("test/cast.lua", "cast", "state", (long)Aria);

        Assert.Empty(_errors);
        Assert.Equal([true], started.Values);
        Assert.Equal([true], casting.Values);
        Assert.Equal([true], stopped.Values);
        Assert.Equal([false], after.Values);
    }

    private void WriteScripts(string root)
    {
        var scripts = Path.Combine(root, "scripts");
        _scripts.Write(
            "common/magic.lua",
            File.ReadAllText(Path.Combine(scripts, "common", "magic.lua"))
                .Replace("magic.random = math.random", "magic.random = function() return ROLL end")
        );

        foreach (var key in new[] { "clumsy", "create_food", "feeblemind", "heal", "magic_arrow", "night_sight", "weaken" })
        {
            var text = File.ReadAllText(Path.Combine(scripts, "spells", key + ".lua"));
            var hook = text.Contains(key + ".random = math.random", StringComparison.Ordinal)
                ? $"\n{key}.random = function(low, high) return math.min(math.max(PICK, low), high) end\n"
                : "";
            _scripts.Write($"spells/{key}.lua", text + hook);
        }

        foreach (var name in new[] { "spellbook", "spell_scroll" })
        {
            _scripts.Write($"items/{name}.lua", File.ReadAllText(Path.Combine(scripts, "items", name + ".lua")));
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
