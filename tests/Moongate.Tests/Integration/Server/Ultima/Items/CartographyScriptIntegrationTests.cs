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
using Moongate.Server.Ultima.Data.MapItems;
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
///     The shipped <c>scripts/items/cartography_tool.lua</c> with the crafting engine: cartography, maps drawn where the cartographer stands.
/// </summary>
public sealed class CartographyScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int Maps = 1;
    private const int LocalMap = 1;
    private const int SeaChart = 2;
    private const int WorldMap = 3;
    private const int CityMap = 4;

    private const int Created = 1044154;
    private const int NoComponents = 1044253;
    private const int Sound = 0x0249;

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
            new ItemTemplate { Id = "mapmakerspen", ItemId = new Serial(0x0FBF), ScriptId = "cartography_tool" },
            new ItemTemplate { Id = "0x14ec_blank_map", ItemId = new Serial(0x14EC), ScriptId = "map_item" },
            new ItemTemplate { Id = "craftedlocalmap", ItemId = new Serial(0x14EC), ScriptId = "map_item" },
            new ItemTemplate { Id = "craftedseachart", ItemId = new Serial(0x14EC), ScriptId = "map_item" },
            new ItemTemplate { Id = "craftedcitymap", ItemId = new Serial(0x14EC), ScriptId = "map_item" },
            new ItemTemplate
            {
                Id = "largeworldmap", ItemId = new Serial(0x14EC), ScriptId = "map_item",
                Tags = new()
                {
                    ["map_x1"] = "0", ["map_y1"] = "0", ["map_x2"] = "5119", ["map_y2"] = "4095", ["map_width"] = "400",
                    ["map_height"] = "400", ["map_facet"] = "0"
                }
            }
        )
    );

    private readonly CraftService _crafts = new(
        new StubDataLoaderService()
            .With(
                new CraftDefinition
                {
                    Id = "cartography", Name = "Cartography", Skill = "cartography", Sound = Sound,
                    Group =
                    [
                        new()
                        {
                            Name = "Maps",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Local map", Item = "craftedlocalmap", SkillMin = 10, SkillMax = 70,
                                    Resources = [new() { Resource = "maps", Amount = 1 }]
                                },
                                new()
                                {
                                    Name = "Sea chart", Item = "craftedseachart", SkillMin = 35, SkillMax = 95,
                                    Resources = [new() { Resource = "maps", Amount = 1 }]
                                },
                                new()
                                {
                                    Name = "World map", Item = "largeworldmap", SkillMin = 39.5, SkillMax = 99.5,
                                    Resources = [new() { Resource = "maps", Amount = 1 }]
                                },
                                new()
                                {
                                    Name = "City map", Item = "craftedcitymap", SkillMin = 25, SkillMax = 85,
                                    Resources = [new() { Resource = "maps", Amount = 1 }]
                                }
                            ]
                        }
                    ]
                }
            )
            .With(new CraftResourceList { Id = "maps", Templates = ["0x14ec_blank_map"] })
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bank = new()
        { Id = new Serial(0x40000003), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };

    private readonly ItemEntity _tools = new()
        { Id = new Serial(0x40000002), TemplateId = "mapmakerspen", ItemId = 0x0FBF, Amount = 1 };

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
        _aria.Location = new Point3D(1500, 1600, 0);

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
            "items/cartography_tool.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "cartography_tool.lua")) +
            """

            local crafting_for_tests = require("common.crafting")

            crafting_for_tests.open = function(user, tool, craft_id, notice)
                mobile.message(user, "opened " .. tostring(notice or ""))
            end

            function cartography_tool.make(serial, user, group, recipe)
                crafting_for_tests.make(user, serial, "cartography", group, recipe)
            end

            function cartography_tool.pick(serial, user, kind, craft_id)
                crafting_for_tests.set_kind(user, kind, craft_id or "cartography")
            end

            function cartography_tool.last(serial, user)
                crafting_for_tests.make_last(user, serial, "cartography")
            end

            function cartography_tool.make_in(serial, user, craft_id, group, recipe)
                crafting_for_tests.make(user, serial, craft_id, group, recipe)
            end

            -- The rolls of the script are the test's: the ones queued, then a high one, which is no exceptional item.
            crafting_for_tests.roll = function() return 0.999 end

            function cartography_tool.set_rolls(serial, ...)
                local rolls = { ... }
                crafting_for_tests.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
            """
        );
        _scripts.Write("common/crafting.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "crafting.lua")));
        _scripts.Write("common/woods.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "woods.lua")));
        _scripts.Write("common/smithy.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "smithy.lua")));
        _scripts.Write("common/heat.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "heat.lua")));
        _scripts.Write(
            "common/cartography.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "cartography.lua"))
        );
        _scripts.Write("common/metals.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "metals.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Cartography, GainFactor = 1.0, Delay = 1 }
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
        _container.Register<IMapDisplayService, MapDisplayService>(Reuse.Singleton);
        _container.AddScriptModule<MapModule>();
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
    public void ThePen_InTheBackpack_OpensTheGumpOfCartography()
    {
        Run(_tools);

        Assert.Empty(_errors);
        Assert.Single(Opened());
    }

    [Fact]
    public void ALocalMap_IsDrawnAroundTheCartographer_ByItsSkill()
    {
        // At the most of the recipe, so it never fails.
        Skill(700);
        Rolls(0.99);
        Carry("0x14ec_blank_map", 0x14EC, 1);

        Call("make", Aria, Maps, LocalMap);
        Fire(1.25);

        Assert.Empty(_errors);
        var map = Assert.Single(Made("craftedlocalmap"));
        // 64 tiles and 2 more a point of skill on each side, in the facet the cartographer is in.
        Assert.True(MapItemProps.TryGetArea(map, null, out var area));
        Assert.Equal(new MapArea(1296, 1396, 1704, 1804, 200, 200, (int)MapType.Trammel), area);
    }

    [Fact]
    public void ASeaChart_OfAMaster_ShowsMoreOfTheSeaOnALargerDrawing()
    {
        Rolls(0.99);
        Carry("0x14ec_blank_map", 0x14EC, 1);

        Call("make", Aria, Maps, SeaChart);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.True(MapItemProps.TryGetArea(Assert.Single(Made("craftedseachart")), null, out var area));
        Assert.Equal(new MapArea(436, 536, 2564, 2664, 354, 354, (int)MapType.Trammel), area);
    }

    [Fact]
    public void AWorldMap_IsDrawnAroundBritain_LargerWithSkill()
    {
        Rolls(0.99);
        Carry("0x14ec_blank_map", 0x14EC, 1);

        Call("make", Aria, Maps, WorldMap);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.True(MapItemProps.TryGetArea(Assert.Single(Made("largeworldmap")), null, out var area));
        // 20 tiles a point of skill around Britain, wherever the cartographer stands; drawn on the facet it opens in.
        Assert.Equal(new MapArea(0, 0, 3472, 3728, 400, 400, (int)MapType.Felucca), area);
    }

    [Fact]
    public void ACityMap_ReachesFartherThanALocalMap()
    {
        Rolls(0.99);
        Carry("0x14ec_blank_map", 0x14EC, 1);

        Call("make", Aria, Maps, CityMap);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.True(MapItemProps.TryGetArea(Assert.Single(Made("craftedcitymap")), null, out var area));
        Assert.Equal(new MapArea(1036, 1136, 1964, 2064, 232, 232, (int)MapType.Trammel), area);
    }

    [Fact]
    public void ALocalMapInIlshenar_StopsAtTheEdgeOfThatWorld()
    {
        Skill(700);
        Rolls(0.99);
        _aria.Map = MapType.Ilshenar;
        _aria.Location = new Point3D(2290, 10, 0);
        Carry("0x14ec_blank_map", 0x14EC, 1);

        Call("make", Aria, Maps, LocalMap);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.True(MapItemProps.TryGetArea(Assert.Single(Made("craftedlocalmap")), null, out var area));
        Assert.Equal(new MapArea(2086, 0, 2303, 214, 200, 200, (int)MapType.Ilshenar), area);
    }

    [Fact]
    public void AMap_IsNeverExceptional_NorMarked()
    {
        Rolls(0.0);
        Carry("0x14ec_blank_map", 0x14EC, 1);

        Call("make", Aria, Maps, LocalMap);
        Fire(1.25);

        Assert.Empty(_errors);
        var map = Assert.Single(Made("craftedlocalmap"));
        Assert.False(map.TryGetProp<int>("quality", out _));
        Assert.False(map.TryGetProp<long>("crafter_id", out _));
        Assert.Equal([Created], Told());
    }

    [Fact]
    public void ADrawingThatFails_StillFinishesTheCraft_AndSaysSo()
    {
        _scripts.Write(
            "common/cartography.lua",
            "return { draw = function() error(\"no ink\") end }"
        );
        Rolls(0.99);
        Carry("0x14ec_blank_map", 0x14EC, 2);

        Call("make", Aria, Maps, LocalMap);
        Fire(1.25);
        Call("make", Aria, Maps, LocalMap);
        Fire(1.25);

        // The second attempt is not refused as busy: the first one ended.
        Assert.Equal(2, Made("craftedlocalmap").Count);
        Assert.Equal(2, _speech.Told.Count(told => told.Text == "You could not finish what you made."));
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skill(int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == SkillType.Cartography);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Cartography, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == SkillType.Cartography);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Cartography, Base = tenths });
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
