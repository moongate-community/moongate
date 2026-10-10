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
///     The shipped <c>scripts/items/cooking_tool.lua</c> with the crafting engine: cooking, baking at an oven and barbecue at a fire.
/// </summary>
public sealed class CookingScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int Ingredients = 1;
    private const int Preparation = 2;
    private const int Baking = 3;
    private const int Barbecue = 4;

    private const int NotAtAnOven = 1044493;
    private const int NotAtAFire = 1044487;
    private const int Created = 1044154;
    private const int NoComponents = 1044253;
    private const int Sound = 0x0057;

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

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With<ItemTemplate>(
            new ItemTemplate { Id = "0x097f_skillet", ItemId = new Serial(0x097F), ScriptId = "cooking_tool" },
            new ItemTemplate { Id = "0x1045_sack_of_flour", ItemId = new Serial(0x1045), Stackable = true },
            new ItemTemplate { Id = "0x0461_oven", ItemId = new Serial(0x0461) },
            new ItemTemplate { Id = "0x09d0_apple", ItemId = new Serial(0x09D0), Stackable = true },
            new ItemTemplate { Id = "unbaked_apple_pie", ItemId = new Serial(0x1042) },
            new ItemTemplate { Id = "0x103d_dough", ItemId = new Serial(0x103D), Stackable = true },
            new ItemTemplate { Id = "0x103b_bread_loaf", ItemId = new Serial(0x103B), Stackable = true },
            new ItemTemplate { Id = "0x097a_raw_fish_steak", ItemId = new Serial(0x097A), Stackable = true },
            new ItemTemplate { Id = "0x097b_fish_steak", ItemId = new Serial(0x097B), Stackable = true }
        )
    );

    private readonly CraftService _crafts = new(
        new StubDataLoaderService()
            .With(
                new CraftDefinition
                {
                    Id = "cooking", Name = "Cooking", Skill = "cooking", Sound = Sound,
                    Group =
                    [
                        new()
                        {
                            Name = "Ingredients",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Dough", Item = "0x103d_dough", SkillMin = 0, SkillMax = 100,
                                    Resources = [new() { Resource = "flour", Amount = 1 }]
                                }
                            ]
                        },
                        new()
                        {
                            Name = "Preparation",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Unbaked apple pie", Item = "unbaked_apple_pie", SkillMin = 0, SkillMax = 100,
                                    Resources = [new() { Resource = "0x103d_dough", Amount = 1 }, new() { Resource = "0x09d0_apple", Amount = 1 }]
                                }
                            ]
                        },
                        new()
                        {
                            Name = "Baking",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Bread loaf", Item = "0x103b_bread_loaf", SkillMin = 0, SkillMax = 100,
                                    Resources = [new() { Resource = "0x103d_dough", Amount = 1 }]
                                }
                            ]
                        },
                        new()
                        {
                            Name = "Barbecue",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Fish steak", Item = "0x097b_fish_steak", SkillMin = 0, SkillMax = 100,
                                    Resources = [new() { Resource = "0x097a_raw_fish_steak", Amount = 1 }]
                                }
                            ]
                        }
                    ]
                }
            )
            .With(
                new CraftResourceList
                {
                    Id = "flour",
                    Templates =
                    [
                        "0x0a1e_bowl_of_flour", "0x1039_sack_of_flour", "0x103a_open_sack_of_flour", "0x1045_sack_of_flour",
                        "0x1046_open_sack_of_flour"
                    ]
                }
            )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bank = new()
        { Id = new Serial(0x40000003), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };

    private readonly ItemEntity _tools = new()
        { Id = new Serial(0x40000002), TemplateId = "0x097f_skillet", ItemId = 0x097F, Amount = 1 };

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

        // At the most of the gloves, so it never fails unless a test lowers the skill; in a smithy at 10, 10.
        Skill(1000);
        _aria.Location = new Point3D(10, 10, 0);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _bank.Equip(new Serial((uint)Aria), LayerType.Bank);
        _tools.PutInContainer(_backpack.Id, new Point2D(10, 10));
        // A saw that has been used already: no draw of its uses in the tests that are not about it.
        _tools.SetProp("uses_remaining", 50L);
        _items.Add([_backpack, _bank, _tools]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // The gump is drawn by another test: here opening it only says so, with the notice it would show.
        _scripts.Write(
            "items/cooking_tool.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "cooking_tool.lua")) +
            """

            local crafting_for_tests = require("common.crafting")

            crafting_for_tests.open = function(user, tool, craft_id, notice)
                mobile.message(user, "opened " .. tostring(notice or ""))
            end

            function cooking_tool.make(serial, user, group, recipe)
                crafting_for_tests.make(user, serial, "cooking", group, recipe)
            end

            function cooking_tool.pick(serial, user, kind, craft_id)
                crafting_for_tests.set_kind(user, kind, craft_id or "cooking")
            end

            function cooking_tool.last(serial, user)
                crafting_for_tests.make_last(user, serial, "cooking")
            end

            function cooking_tool.make_in(serial, user, craft_id, group, recipe)
                crafting_for_tests.make(user, serial, craft_id, group, recipe)
            end

            -- The rolls of the script are the test's: the ones queued, then a high one, which is no exceptional item.
            crafting_for_tests.roll = function() return 0.999 end

            function cooking_tool.set_rolls(serial, ...)
                local rolls = { ... }
                crafting_for_tests.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
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
            new SkillContent { Id = SkillType.Cooking, GainFactor = 1.0, Delay = 1 }
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
    public void TheSkillet_InTheBackpack_OpensTheGumpOfCooking()
    {
        Run(_tools);

        Assert.Empty(_errors);
        Assert.Single(Opened());
    }

    [Fact]
    public void Dough_IsMadeAnywhere_FromTheClosedSackOfTheStartingItems()
    {
        Carry("0x1045_sack_of_flour", 0x1045, 1);

        Call("make", Aria, Ingredients, 1);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Single(Made("0x103d_dough"));
    }

    [Fact]
    public void Bread_IsBakedOnlyAtAnOven()
    {
        var dough = Carry("0x103d_dough", 0x103D, 2);

        Call("make", Aria, Baking, 1);

        Assert.Equal([NotAtAnOven], Told());
        Assert.Equal(2, Left(dough));

        // A campfire cooks, it does not bake.
        _map.AddStatic(11, 10, 0x0DE3, 0);
        Call("make", Aria, Baking, 1);
        Assert.Equal([NotAtAnOven, NotAtAnOven], Told());

        _map.AddStatic(10, 11, 0x0461, 0);
        Call("make", Aria, Baking, 1);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Single(Made("0x103b_bread_loaf"));
    }

    [Fact]
    public void AFishSteak_IsCookedAtAFire()
    {
        Carry("0x097a_raw_fish_steak", 0x097A, 1);

        Call("make", Aria, Barbecue, 1);

        Assert.Equal([NotAtAFire], Told());

        _map.AddStatic(11, 11, 0x0DE3, 0);
        Call("make", Aria, Barbecue, 1);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Single(Made("0x097b_fish_steak"));
    }

    [Fact]
    public void AnApplePie_IsPreparedAnywhere()
    {
        Carry("0x103d_dough", 0x103D, 1);
        Carry("0x09d0_apple", 0x09D0, 1);

        Call("make", Aria, Preparation, 1);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Single(Made("unbaked_apple_pie"));
    }

    [Fact]
    public void TheOvenGoneBeforeTheSecondStroke_BakesNothing()
    {
        var oven = Ground("0x0461_oven", 0x0461, 11, 10);
        var dough = Carry("0x103d_dough", 0x103D, 1);

        Call("make", Aria, Baking, 1);
        _items.Remove([oven.Id]);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NotAtAnOven], Told());
        Assert.Equal(1, Left(dough));
        Assert.Empty(Made("0x103b_bread_loaf"));
    }

    [Fact]
    public void MakeLast_OfBread_AwayFromTheOven_IsRefused()
    {
        var oven = Ground("0x0461_oven", 0x0461, 11, 10);
        Carry("0x103d_dough", 0x103D, 2);
        Call("make", Aria, Baking, 1);
        Fire(1.25);
        _items.Remove([oven.Id]);

        Call("last", Aria);

        Assert.Empty(_errors);
        Assert.Equal(NotAtAnOven, Told().Last());
        Assert.Single(Made("0x103b_bread_loaf"));
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skill(int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == SkillType.Cooking);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Cooking, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == SkillType.Cooking);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Cooking, Base = tenths });
    }

    private void Make(int recipe)
    {
        Call("make", Aria, 1, recipe);
    }

    private void Rolls(params double[] rolls)
    {
        Call("set_rolls", rolls.Cast<object?>().ToArray());
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
