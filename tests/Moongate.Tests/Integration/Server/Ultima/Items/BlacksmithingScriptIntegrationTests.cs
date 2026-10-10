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
///     The shipped <c>scripts/items/smithing_tool.lua</c> with the crafting engine: blacksmithing at an anvil and a forge.
/// </summary>
public sealed class BlacksmithingScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int Gloves = 1;

    private const int InBackpack = 1062334;
    private const int Created = 1044154;
    private const int Exceptional = 1044155;
    private const int Marked = 1044156;
    private const int NoMetal = 1044037;
    private const int NotAtTheForge = 1044267;
    private const int NoIdea = 1044268;
    private const int Sound = 0x002A;

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
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "0x13e3", ItemId = new Serial(0x13E3), ScriptId = "smithing_tool" },
            new ItemTemplate { Id = "0x1bf2_iron_ingot", ItemId = new Serial(0x1BF2), Stackable = true },
            new ItemTemplate { Id = "0x0faf_anvil", ItemId = new Serial(0x0FAF) },
            new ItemTemplate { Id = "0x0fb1_forge", ItemId = new Serial(0x0FB1) },
            new ItemTemplate { Id = "0x13eb_ringmail_gloves", ItemId = new Serial(0x13EB) },
            new ItemTemplate { Id = "ingot_copper", ItemId = new Serial(0x1BF2), Stackable = true },
            new ItemTemplate { Id = "ingot_valorite", ItemId = new Serial(0x1BF2), Stackable = true },
            new ItemTemplate { Id = "0x1bd7_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "0x1db8_keg", ItemId = new Serial(0x1DB8) }
        )
    );

    private readonly CraftService _crafts = new(
        new StubDataLoaderService()
            .With(
                new CraftDefinition
                {
                    Id = "blacksmithing", Name = "Blacksmithing", Skill = "blacksmithy", Sound = Sound,
                    Group =
                    [
                        new()
                        {
                            Name = "Ringmail",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Ringmail gloves", Item = "0x13eb_ringmail_gloves", SkillMin = 12.2, SkillMax = 37.2,
                                    Resources = [new() { Resource = "metal", Amount = 10 }]
                                },
                                new()
                                {
                                    Name = "Keg", Item = "0x1db8_keg", SkillMin = 0, SkillMax = 10,
                                    Resources = [new() { Resource = "metal", Amount = 2 }, new() { Resource = "wood", Amount = 3 }]
                                }
                            ]
                        }
                    ]
                },
                new CraftDefinition
                {
                    Id = "carpentry", Name = "Carpentry", Skill = "carpentry", Sound = Sound,
                    Group =
                    [
                        new()
                        {
                            Name = "Boxes",
                            Recipe = [new() { Name = "Box", Item = "0x1db8_keg", SkillMin = 0, SkillMax = 10, Resources = [new() { Resource = "wood", Amount = 1 }] }]
                        }
                    ]
                }
            )
            .With(
                new CraftResourceList { Id = "metal", Templates = ["0x1bf2_iron_ingot"] },
                new CraftResourceList { Id = "wood", Templates = ["0x1bd7_board"] }
            )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bank = new()
        { Id = new Serial(0x40000003), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };

    private readonly ItemEntity _hammer = new()
        { Id = new Serial(0x40000002), TemplateId = "0x13e3", ItemId = 0x13E3, Amount = 1 };

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
        Skill(372);
        _aria.Location = new Point3D(10, 10, 0);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _bank.Equip(new Serial((uint)Aria), LayerType.Bank);
        _hammer.PutInContainer(_backpack.Id, new Point2D(10, 10));
        // A saw that has been used already: no draw of its uses in the tests that are not about it.
        _hammer.SetProp("uses_remaining", 50L);
        _items.Add([_backpack, _bank, _hammer]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // The gump is drawn by another test: here opening it only says so, with the notice it would show.
        _scripts.Write(
            "items/smithing_tool.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "smithing_tool.lua")) +
            """

            local crafting_for_tests = require("common.crafting")

            crafting_for_tests.open = function(user, tool, craft_id, notice)
                mobile.message(user, "opened " .. tostring(notice or ""))
            end

            function smithing_tool.make(serial, user, group, recipe)
                crafting_for_tests.make(user, serial, "blacksmithing", group, recipe)
            end

            function smithing_tool.pick(serial, user, kind, craft_id)
                crafting_for_tests.set_kind(user, kind, craft_id or "blacksmithing")
            end

            function smithing_tool.kind_of(serial, user, craft_id)
                mobile.message(user, "kind " .. crafting_for_tests.kind(user, craft_id))
            end

            function smithing_tool.last(serial, user)
                crafting_for_tests.make_last(user, serial, "blacksmithing")
            end

            function smithing_tool.make_in(serial, user, craft_id, group, recipe)
                crafting_for_tests.make(user, serial, craft_id, group, recipe)
            end

            -- The rolls of the script are the test's: the ones queued, then a high one, which is no exceptional item.
            crafting_for_tests.roll = function() return 0.999 end

            function smithing_tool.set_rolls(serial, ...)
                local rolls = { ... }
                crafting_for_tests.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
            """
        );
        _scripts.Write("common/crafting.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "crafting.lua")));
        _scripts.Write("common/woods.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "woods.lua")));
        _scripts.Write("common/smithy.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "smithy.lua")));
        _scripts.Write("common/metals.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "metals.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Blacksmithy, GainFactor = 1.0, Delay = 1 }
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
    public void TheHammer_InTheBackpack_OpensTheGumpOfBlacksmithing()
    {
        Run(_hammer);

        Assert.Empty(_errors);
        Assert.Single(Opened());
    }

    [Fact]
    public void AwayFromAnAnvilAndAForge_NothingIsForged_AndNothingTaken()
    {
        var ingots = Carry("0x1bf2_iron_ingot", 0x1BF2, 20);

        Make();

        Assert.Empty(_errors);
        Assert.Equal([NotAtTheForge], Told());
        Assert.Equal(20, ingots.Amount);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void AForgeWithoutAnAnvil_IsNotEnough()
    {
        _map.AddStatic(11, 10, 0x0FB1, 0);
        Carry("0x1bf2_iron_ingot", 0x1BF2, 20);

        Make();

        Assert.Equal([NotAtTheForge], Told());
    }

    [Fact]
    public void AnAnvilOnTheGround_AndAForgeOfTheMap_WithinTwoTiles_LetTheSmithForge()
    {
        Ground("0x0faf_anvil", 0x0FAF, 12, 10);
        _map.AddStatic(10, 8, 0x1985, 0);
        var ingots = Carry("0x1bf2_iron_ingot", 0x1BF2, 20);

        Make();
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([Created], Told());
        Assert.Equal(10, ingots.Amount);
        Assert.Single(Made("0x13eb_ringmail_gloves"));
    }

    [Fact]
    public void AnAnvilThreeTilesAway_IsTooFar()
    {
        Ground("0x0faf_anvil", 0x0FAF, 13, 10);
        _map.AddStatic(11, 10, 0x0FB1, 0);
        Carry("0x1bf2_iron_ingot", 0x1BF2, 20);

        Make();

        Assert.Equal([NotAtTheForge], Told());
    }

    [Fact]
    public void TheAnvilGoneBeforeTheSecondStroke_ForgesNothing()
    {
        var anvil = Ground("0x0faf_anvil", 0x0FAF, 11, 10);
        _map.AddStatic(11, 11, 0x0FB1, 0);
        var ingots = Carry("0x1bf2_iron_ingot", 0x1BF2, 20);

        Make();
        _items.Remove([anvil.Id]);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NotAtTheForge], Told());
        Assert.Equal(20, ingots.Amount);
        Assert.Empty(Made("0x13eb_ringmail_gloves"));
    }

    [Fact]
    public void WithoutIngots_SaysSo()
    {
        AtTheForge();

        Make();

        Assert.Equal([NoMetal], Told());
    }

    [Theory]
    // At the most of the gloves an exceptional pair is uncommon; made at 100 it bears the mark and is rare.
    [InlineData(372, Exceptional, "Uncommon")]
    [InlineData(1000, Marked, "Rare")]
    public void AnExceptionalPiece_TakesARarityFromItsQuality(int tenths, int told, string rarity)
    {
        AtTheForge();
        Skill(tenths);
        Carry("0x1bf2_iron_ingot", 0x1BF2, 10);
        Rolls(0.0);

        Make();
        Fire(1.25);

        var gloves = Assert.Single(Made("0x13eb_ringmail_gloves"));
        Assert.Empty(_errors);
        Assert.Equal([told], Told());
        Assert.Equal(Enum.Parse<ItemRarityType>(rarity), gloves.Rarity);
    }

    [Fact]
    public void ARegularPiece_KeepsItsRarity()
    {
        AtTheForge();
        Carry("0x1bf2_iron_ingot", 0x1BF2, 10);

        Make();
        Fire(1.25);

        Assert.Equal(ItemRarityType.Common, Assert.Single(Made("0x13eb_ringmail_gloves")).Rarity);
    }

    [Fact]
    public void AKindOfWoodPickedForCarpentry_DoesNotStopTheSmith()
    {
        AtTheForge();
        SetSkill(SkillType.Carpentry, 650);
        Call("pick", Aria, "oak", "carpentry");
        Call("pick", Aria, "copper", "blacksmithing");
        Carry("0x1bf2_iron_ingot", 0x1BF2, 10);
        Call("kind_of", Aria, "carpentry");
        Call("kind_of", Aria, "blacksmithing");

        // Each craft keeps its own pick: oak for carpentry; copper was refused to a smith of 37.2, iron stays.
        Assert.Equal(["kind oak", "kind iron"], Kinds());

        Make();
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NoIdea, Created], Told());
    }

    [Fact]
    public void ARecipeOfMetalAndWood_TakesTheMetalPicked_AndPlainWood_AndTakesTheColourOfTheMetal()
    {
        AtTheForge();
        Skill(1000);
        var copper = Carry("ingot_copper", 0x1BF2, 2);
        copper.Hue = new Hue(0x96D);
        var boards = Carry("0x1bd7_board", 0x1BD7, 3);

        Call("pick", Aria, "copper");
        Call("make", Aria, 1, 2);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([Created], Told());
        Assert.Equal((0, 0), (Left(copper), Left(boards)));
        Assert.Equal(new Hue(0x96D), Assert.Single(Made("0x1db8_keg")).Hue);
    }

    [Fact]
    public void ARecipeOfMetalAndWood_WithNothingPicked_TakesIronAndPlainWood()
    {
        AtTheForge();
        Carry("0x1bf2_iron_ingot", 0x1BF2, 2);
        Carry("0x1bd7_board", 0x1BD7, 3);

        Call("make", Aria, 1, 2);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([Created], Told());
        Assert.Single(Made("0x1db8_keg"));
    }

    [Fact]
    public void AnAnvilOnAnotherFloor_IsNoAnvil()
    {
        Ground("0x0faf_anvil", 0x0FAF, 11, 10, 30);
        _map.AddStatic(9, 10, 0x0FB1, 0);
        Carry("0x1bf2_iron_ingot", 0x1BF2, 10);

        Make();

        Assert.Equal([NotAtTheForge], Told());
    }

    [Fact]
    public void TheForgeAndTheIngotsGoneBeforeTheSecondStroke_SayTheForge_AndNeitherTryNorWear()
    {
        var anvil = Ground("0x0faf_anvil", 0x0FAF, 11, 10);
        _map.AddStatic(11, 11, 0x0FB1, 0);
        var ingots = Carry("0x1bf2_iron_ingot", 0x1BF2, 10);
        Skill(200);

        Make();
        _items.Remove([anvil.Id]);
        _items.MoveToContainer(ingots, _bank.Id, new Point2D(5, 5));
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NotAtTheForge], Told());
        Assert.Equal(0, _random.Rolls);
        Assert.True(_hammer.TryGetProp<int>("uses_remaining", out var left));
        Assert.Equal(50, left);
    }

    [Fact]
    public void ACopperPiece_AsksForItsBlacksmithy_TakesCopperIngots_AndIsCopperColoured()
    {
        AtTheForge();
        Skill(749);
        var copper = Carry("ingot_copper", 0x1BF2, 10);
        copper.Hue = new Hue(0x96D);
        var iron = Carry("0x1bf2_iron_ingot", 0x1BF2, 10);

        Call("pick", Aria, "copper");

        Assert.Equal([NoIdea], Told());

        Skill(750);
        Call("pick", Aria, "copper");
        _random.Doubles(0.0);
        Make();
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NoIdea, Created], Told());
        Assert.Equal((0, 10), (Left(copper), iron.Amount));
        Assert.Equal(new Hue(0x96D), Assert.Single(Made("0x13eb_ringmail_gloves")).Hue);
    }

    [Fact]
    public void WithoutIngotsOfTheMetalPicked_SaysTheMetalIsLacking()
    {
        AtTheForge();
        Skill(1000);
        Carry("0x1bf2_iron_ingot", 0x1BF2, 10);

        Call("pick", Aria, "valorite");
        Make();

        Assert.Equal([NoMetal], Told());
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void AtTheForge()
    {
        Ground("0x0faf_anvil", 0x0FAF, 11, 10);
        _map.AddStatic(9, 10, 0x0FB1, 0);
    }

    private void Skill(int tenths)
    {
        SetSkill(SkillType.Blacksmithy, tenths);
    }

    // The skill service reads the mobile, the mobile module the state service: both hold the same.
    private void SetSkill(SkillType skill, int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == skill);
        _aria.Skills.Add(new MobileSkill { Skill = skill, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == skill);
        _state.Skills.Add(new MobileSkill { Skill = skill, Base = tenths });
    }

    private void Make()
    {
        Call("make", Aria, 1, Gloves);
    }

    private void Rolls(params double[] rolls)
    {
        Call("set_rolls", rolls.Cast<object?>().ToArray());
    }

    private void Call(string function, params object?[] args)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_hammer, function, args);

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

    private ItemEntity Ground(string template, int graphic, int x, int y, int z = 0)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, _aria.Map, new Point3D(x, y, z));

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

    private List<string> Kinds()
    {
        return _speech.Told.Where(told => told.Player == _aria && told.Text.StartsWith("kind", StringComparison.Ordinal))
            .Select(told => told.Text)
            .ToList();
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
