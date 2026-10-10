using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Containers;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.HuePicking;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/inscription_tool.lua</c> with the crafting engine and the shipped inscription recipes: a scroll of
///     a spell from the reagents, a blank scroll, the spell in a carried book and the mana of its circle.
/// </summary>
public sealed class InscriptionScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int NoSpell = 1042404;
    private const int NoMana = 502625;
    private const int NoComponents = 1044253;
    private const int NoSkill = 1044153;
    private const int Inscribed = 501629;
    private const int Ruined = 501630;
    private const int Sound = 0x0249;

    private static readonly int[] SpellCircleMana = [4, 6, 9, 11, 14, 20, 40, 50];

    private const string BlankScroll = "0x0e34_a_blank_scroll";
    private const string ClumsyScroll = "0x1f2e_clumsy_scroll";

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubTargetService _targets = new();
    private readonly ScriptedRandom _random = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingEffectService _effects = new();
    private readonly StubMovementService _movement = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly StubContainerCapacityService _capacity = new();
    private readonly StubInventoryMutationGuard _guard = new();
    private readonly SettableClock _time = new();
    private readonly FakeMapService _map = new(32, 32, MapType.Trammel);
    private ItemService _items = null!;

    private ItemTemplateService _templates = null!;
    private CraftService _crafts = null!;
    private CraftDefinition _craft = null!;

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bank = new()
        { Id = new Serial(0x40000003), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };

    private readonly ItemEntity _tools = new()
        { Id = new Serial(0x40000002), TemplateId = "0x0fc0_pen_and_ink", ItemId = 0x0FC0, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private readonly HashSet<string> _fired = [];
    private uint _next = 0x40000050;


    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        // The sectors world.items_in_range reads: an anvil on the ground must be found there.
        _items = TestItems.Create(_fixture.Sectors);
        await _fixture.AddAsync((int)Aria);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _aria.AccountId = new Serial(0x42);

        // At the most of every circle, so it never fails unless a test lowers the skill.
        Skill(1250);
        (_aria.Mana, _aria.ManaMax, _aria.Intelligence) = (30, 30, 30);
        _aria.Location = new Point3D(10, 10, 0);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _bank.Equip(new Serial((uint)Aria), LayerType.Bank);
        _tools.PutInContainer(_backpack.Id, new Point2D(10, 10));
        // A pen that has been used already: no draw of its uses in the tests that are not about it.
        _tools.SetProp("uses_remaining", 50L);
        _items.Add([_backpack, _bank, _tools]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        var directories = new DirectoriesConfig(root, ["data", "templates"]);
        var shipped = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        _templates = new(new StubDataLoaderService().With(shipped));
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(shipped)).LoadDataAsync()).Entities;
        var catalog = new SpellCatalogService(new StubDataLoaderService().With(spells.ToArray()), _templates);
        var loaded = new StubDataLoaderService().With(shipped);
        var lists = (await new CraftResourcesLoader(directories, loaded).LoadDataAsync()).Entities.ToArray();
        loaded.With(lists);
        loaded.With(spells.ToArray());
        _craft = (await new CraftsLoader(directories, loaded).LoadDataAsync()).Entities.Single(craft => craft.Id == "inscription");
        _crafts = new(new StubDataLoaderService().With(_craft).With(lists));

        // The gump is drawn by another test: here opening it only says so, with the notice it would show.
        _scripts.Write(
            "items/inscription_tool.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "inscription_tool.lua")) +
            """

            local crafting_for_tests = require("common.crafting")

            crafting_for_tests.open = function(user, tool, craft_id, notice)
                mobile.message(user, "opened " .. tostring(notice or ""))
            end

            function inscription_tool.make(serial, user, group, recipe)
                crafting_for_tests.make(user, serial, "inscription", group, recipe)
            end

            function inscription_tool.last(serial, user)
                crafting_for_tests.make_last(user, serial, "inscription")
            end

            -- The rolls of the script are the test's: the ones queued, then a high one.
            crafting_for_tests.roll = function() return 0.999 end
            """
        );
        _scripts.Write("common/crafting.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "crafting.lua")));
        _scripts.Write("common/woods.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "woods.lua")));
        _scripts.Write("common/smithy.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "smithy.lua")));
        _scripts.Write("common/heat.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "heat.lua")));
        _scripts.Write("common/metals.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "metals.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Inscription, GainFactor = 1.0, Delay = 1 }
        );

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
        _container.RegisterInstance<ISkillService>(new SkillService(_state, data, new SkillsConfig(), _random));
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
        _container.RegisterInstance<ITileDataService>(new FakeTileDataService());
        _container.RegisterInstance<IContainerCapacityService>(_capacity);
        _container.RegisterInstance<IInventoryMutationGuard>(_guard);
        _container.RegisterInstance<TimeProvider>(_time);
        _container.RegisterInstance<IMapService>(_map);
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterInstance<ICraftService>(_crafts);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IMovementService>(_movement);
        _container.RegisterInstance<IDeathService>(new StubDeathService());
        _container.RegisterInstance<IEffectService>(_effects);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<CraftModule>();
        _container.RegisterInstance<ISpellCatalogService>(catalog);
        _container.Register<ISpellbookService, SpellbookService>(Reuse.Singleton);
        _container.RegisterDelegate<ISpellCastService>(_ => null!);
        _container.AddScriptModule<SpellModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        _itemScripts = new(_engine, _templates, _loop, options);
        await _itemScripts.StartAsync();
    }

    [Fact]
    public void ThePenAndInk_InTheBackpack_OpensTheGumpOfInscription()
    {
        Run(_tools);

        Assert.Empty(_errors);
        Assert.Single(Opened());
    }

    [Fact]
    public void AScroll_IsWrittenFromTheReagentsAndABlankScroll_AndTakesTheManaOfItsCircleOnce()
    {
        Book("clumsy");
        var moss = Carry("0x0f7b_10_blood_moss", 0x0F7B, 5);
        var shade = Carry("0x0f88_nightshade", 0x0F88, 2);
        var blank = Carry(BlankScroll, 0x0E34, 3);

        Make("clumsy");
        Fire(1.25);

        Assert.Empty(_errors);
        var scroll = Assert.Single(Made(ClumsyScroll));
        Assert.False(scroll.TryGetProp<int>("quality", out _));
        Assert.Equal((4, 1, 2), (Left(moss), Left(shade), Left(blank)));
        Assert.Equal(26, _aria.Mana);
        Assert.Equal([Inscribed], Told());
        Assert.Contains(_speech.Sounds, sound => sound.Source == _aria && sound.Sound == Sound);
        Assert.True(_tools.TryGetProp<int>("uses_remaining", out var uses));
        Assert.Equal(49, uses);
    }

    [Fact]
    public void ASpellThatIsInNoBook_IsRefusedBeforeAnythingIsSpent()
    {
        Book("heal");
        var moss = Carry("0x0f7b_blood_moss", 0x0F7B, 1);
        var shade = Carry("0x0f88_nightshade", 0x0F88, 1);
        var blank = Carry(BlankScroll, 0x0E34, 1);

        Make("clumsy");

        Assert.Equal([NoSpell], Told());
        Assert.Equal((1, 1, 1), (Left(moss), Left(shade), Left(blank)));
        Assert.Equal(30, _aria.Mana);
        Assert.Empty(_timers.Timers);
        Assert.Empty(Made(ClumsyScroll));
    }

    [Fact]
    public void ABookInABagOfTheBackpack_IsNotCarried_SoTheSpellIsMissing()
    {
        var bag = Carry("0x0e76_bag", 0x0E76, 1);
        var book = new ItemEntity { Id = new Serial(_next++), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        book.SetProp("spellbook.spells", (long)(1UL << (SpellId("clumsy") - 1)));
        book.PutInContainer(bag.Id, new Point2D(5, 5));
        _items.Add([book]);
        Carry("0x0f7b_blood_moss", 0x0F7B, 1);
        Carry("0x0f88_nightshade", 0x0F88, 1);
        Carry(BlankScroll, 0x0E34, 1);

        Make("clumsy");

        Assert.Equal([NoSpell], Told());
    }

    [Fact]
    public void WithoutTheReagents_ThereAreNoComponents_AndNothingIsSpent()
    {
        Book("clumsy");
        var blank = Carry(BlankScroll, 0x0E34, 1);
        Carry("0x0f7b_blood_moss", 0x0F7B, 1);

        Make("clumsy");

        Assert.Equal([NoComponents], Told());
        Assert.Equal(1, Left(blank));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void WithoutTheMana_TheTryIsRefused_AndNothingIsSpent()
    {
        Book("clumsy");
        _aria.Mana = 3;
        var moss = Carry("0x0f7b_blood_moss", 0x0F7B, 1);
        var shade = Carry("0x0f88_nightshade", 0x0F88, 1);
        var blank = Carry(BlankScroll, 0x0E34, 1);

        Make("clumsy");

        Assert.Equal([NoMana], Told());
        Assert.Equal((1, 1, 1), (Left(moss), Left(shade), Left(blank)));
        Assert.Equal(3, _aria.Mana);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void ManaSpentBetweenTheStrokes_RefusesTheSecondStroke_WithNothingTaken()
    {
        Book("clumsy");
        var moss = Carry("0x0f7b_blood_moss", 0x0F7B, 1);
        var shade = Carry("0x0f88_nightshade", 0x0F88, 1);
        var blank = Carry(BlankScroll, 0x0E34, 1);

        Make("clumsy");
        _aria.Mana = 0;
        Fire(1.25);

        Assert.Equal([NoMana], Told());
        Assert.Equal((1, 1, 1), (Left(moss), Left(shade), Left(blank)));
        Assert.Empty(Made(ClumsyScroll));
    }

    [Fact]
    public void TheBookGivenAwayBetweenTheStrokes_RefusesTheSecondStroke_WithNothingTaken()
    {
        var book = Book("clumsy");
        var blank = Carry(BlankScroll, 0x0E34, 1);
        Carry("0x0f7b_blood_moss", 0x0F7B, 1);
        Carry("0x0f88_nightshade", 0x0F88, 1);

        Make("clumsy");
        _items.Remove([book.Id]);
        Fire(1.25);

        Assert.Equal([NoSpell], Told());
        Assert.Equal(1, Left(blank));
        Assert.Equal(30, _aria.Mana);
    }

    [Fact]
    public void AFailure_RuinsOneOfEveryResourceTheBlankScrollToo_AndTakesNoMana()
    {
        Skill(0);
        _random.Doubles(0.99);
        Book("clumsy");
        var moss = Carry("0x0f7b_blood_moss", 0x0F7B, 2);
        var shade = Carry("0x0f88_nightshade", 0x0F88, 2);
        var blank = Carry(BlankScroll, 0x0E34, 2);

        Make("clumsy");
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Empty(Made(ClumsyScroll));
        // One unit of every resource, the blank scroll too: the scroll is ruined. The mana is paid by a success only.
        Assert.Equal((1, 1, 1), (Left(moss), Left(shade), Left(blank)));
        Assert.Equal(30, _aria.Mana);
        Assert.Equal([Ruined], Told());
    }

    [Fact]
    public void ACircleBeyondTheSkill_IsRefused_AndAFirstCircleScrollIsTriedAtZero()
    {
        Skill(0);
        Book("clumsy", "fireball", "summon_water_elemental");
        Carry("0x0f7a_black_pearl", 0x0F7A, 1);
        Carry("0x0f7b_blood_moss", 0x0F7B, 1);
        Carry("0x0f88_nightshade", 0x0F88, 1);
        Carry(BlankScroll, 0x0E34, 1);

        Make("summon_water_elemental");

        Assert.Equal([NoSkill], Told());
        Assert.Empty(_timers.Timers);

        Make("clumsy");

        Assert.Single(_timers.Timers);
    }

    [Fact]
    public void EveryRecipe_NamesAShippedSpell_WithTheManaOfItsCircle()
    {
        var recipes = _craft.Group.SelectMany(group => group.Recipe).ToList();

        Assert.Equal(64, recipes.Count);
        Assert.All(
            recipes,
            recipe => Assert.True(
                _container.Resolve<ISpellCatalogService>().TryGetByKey(recipe.Spell, out var spell) &&
                spell.Scroll == recipe.Item &&
                SpellCircleMana[spell.Circle - 1] == recipe.Mana,
                recipe.Name
            )
        );
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skill(int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == SkillType.Inscription);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Inscription, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == SkillType.Inscription);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Inscription, Base = tenths });
    }

    // Makes the scroll of a spell by the position of its recipe in the shipped craft.
    private void Make(string spell)
    {
        for (var group = 0; group < _craft.Group.Count; group++)
        {
            var index = _craft.Group[group].Recipe.FindIndex(recipe => recipe.Spell == spell);

            if (index >= 0)
            {
                Call("make", Aria, group + 1, index + 1);

                return;
            }
        }

        Assert.Fail($"No recipe of {spell}");
    }

    // A spellbook in the backpack holding those spells.
    private ItemEntity Book(params string[] keys)
    {
        var mask = keys.Aggregate(0UL, (all, key) => all | 1UL << (SpellId(key) - 1));
        var book = new ItemEntity { Id = new Serial(_next++), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        book.SetProp("spellbook.spells", unchecked((long)mask));
        book.PutInContainer(_backpack.Id, new Point2D(20, 20));
        _items.Add([book]);

        return book;
    }

    private int SpellId(string key)
    {
        return _container.Resolve<ISpellCatalogService>().TryGetByKey(key, out var spell) ? spell.Id : 0;
    }

    private void Call(string function, params object?[] args)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_tools, function, args);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private void Run(ItemEntity tool)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(tool, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    // Fires the oldest timer of that many seconds that has not fired yet.
    private void Fire(double seconds)
    {
        var timer = _timers.Timers.First(timer => !_fired.Contains(timer.Id) &&
                                                  Math.Abs(timer.Interval.TotalSeconds - seconds) < 0.001
        );
        _fired.Add(timer.Id);
        _loop.DeferTryPost = true;
        _timers.Fire(timer.Id);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private ItemEntity Carry(string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([item]);

        return item;
    }

    private ItemEntity Ground(string template, int graphic, int x, int y)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, _aria.Map, new Point3D(x, y, 0));

        return item;
    }

    private int Left(ItemEntity stack)
    {
        return _items.TryGet(stack.Id, out var still) ? still.Amount : 0;
    }

    // The items of a template made from the serials the pool gives.
    private List<ItemEntity> Made(string template)
    {
        var made = new List<ItemEntity>();

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            if (_items.TryGet(new Serial(serial), out var item) && item.TemplateId == template)
            {
                made.Add(item);
            }
        }

        return made;
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player == _aria).Select(told => told.Cliloc).ToList();
    }

    private List<string> Opened()
    {
        return _speech.Told.Where(told => told.Player == _aria && told.Text.StartsWith("opened", StringComparison.Ordinal))
            .Select(told => told.Text.TrimEnd())
            .ToList();
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
