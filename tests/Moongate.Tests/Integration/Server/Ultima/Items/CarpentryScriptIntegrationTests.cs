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
///     The shipped <c>scripts/items/carpentry_tool.lua</c> and <c>scripts/common/crafting.lua</c>, with the real Lua
///     engine, the real skill service and the craft service: what one attempt to make an item does.
/// </summary>
public sealed class CarpentryScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int Stool = 1;
    private const int Lute = 2;
    private const int Staves = 3;
    private const int Peg = 4;

    private const int InBackpack = 1062334;
    private const int Created = 1044154;
    private const int FailedAndLost = 1044043;
    private const int NoSkill = 1044153;
    private const int NoWood = 1044351;
    private const int NoCloth = 1044287;
    private const int StrangeWood = 1072652;
    private const int Busy = 500119;
    private const int Sound = 0x023D;
    private const int Exceptional = 1044155;
    private const int Marked = 1044156;
    private const int WornOut = 1044038;
    private const int NothingYet = 1044165;

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
    private readonly ItemService _items;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "0x1034_saw", ItemId = new Serial(0x1034), ScriptId = "carpentry_tool" },
            new ItemTemplate { Id = "0x1bd7_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "0x1bda_board", ItemId = new Serial(0x1BDA), Stackable = true },
            new ItemTemplate { Id = "oak_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "0x175d_cloth", ItemId = new Serial(0x175D), Stackable = true },
            new ItemTemplate { Id = "0x0a2b", ItemId = new Serial(0x0A2B) },
            new ItemTemplate { Id = "0x0eb3_lute", ItemId = new Serial(0x0EB3) },
            new ItemTemplate { Id = "0x1eb1_barrel_staves", ItemId = new Serial(0x1EB1), Stackable = true },
            new ItemTemplate { Id = "0x14f0_peg", ItemId = new Serial(0x14F0) }
        )
    );

    private readonly CraftService _crafts = new(
        new StubDataLoaderService()
            .With(
                new CraftDefinition
                {
                    Id = "carpentry", Name = "Carpentry", Skill = "carpentry", Sound = Sound,
                    Group =
                    [
                        new()
                        {
                            Name = "Chairs",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Stool", Item = "0x0a2b", SkillMin = 11, SkillMax = 36,
                                    Resources = [new() { Resource = "wood", Amount = 9 }]
                                },
                                new()
                                {
                                    Name = "Lute", Item = "0x0eb3_lute", SkillMin = 68.4, SkillMax = 93.4,
                                    Resources = [new() { Resource = "wood", Amount = 25 }, new() { Resource = "cloth", Amount = 10 }],
                                    Skills = [new() { Skill = "musicianship", Min = 45, Max = 70 }]
                                },
                                new()
                                {
                                    Name = "Barrel Staves", Item = "0x1eb1_barrel_staves", SkillMin = 0, SkillMax = 25,
                                    Resources = [new() { Resource = "wood", Amount = 5 }]
                                },
                                new()
                                {
                                    Name = "Peg", Item = "0x14f0_peg", SkillMin = 0, SkillMax = 50,
                                    Resources = [new() { Resource = "wood", Amount = 1 }]
                                }
                            ]
                        }
                    ]
                }
,
                new CraftDefinition
                {
                    Id = "woodwork", Name = "Woodwork", Skill = "carpentry", Sound = Sound,
                    Group =
                    [
                        new()
                        {
                            Name = "Small",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Peg", Item = "0x14f0_peg", SkillMin = 0, SkillMax = 10,
                                    Resources = [new() { Resource = "wood", Amount = 1 }]
                                }
                            ]
                        }
                    ]
                }
            )
            .With(
                new CraftResourceList { Id = "wood", Templates = ["0x1bd7_board", "0x1bda_board"] },
                new CraftResourceList { Id = "cloth", Templates = ["0x175d_cloth"] }
            )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bank = new()
        { Id = new Serial(0x40000003), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };

    private readonly ItemEntity _saw = new()
        { Id = new Serial(0x40000002), TemplateId = "0x1034_saw", ItemId = 0x1034, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private readonly HashSet<string> _fired = [];
    private uint _next = 0x40000050;

    public CarpentryScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync((int)Aria);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _aria.AccountId = new Serial(0x42);

        // At the most of the stool, so it never fails unless a test lowers the skill.
        Skill(360);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _bank.Equip(new Serial((uint)Aria), LayerType.Bank);
        _saw.PutInContainer(_backpack.Id, new Point2D(10, 10));
        // A saw that has been used already: no draw of its uses in the tests that are not about it.
        _saw.SetProp("uses_remaining", 50L);
        _items.Add([_backpack, _bank, _saw]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // The gump is drawn by another test: here opening it only says so, with the notice it would show.
        _scripts.Write(
            "items/carpentry_tool.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "carpentry_tool.lua")) +
            """

            local crafting_for_tests = require("common.crafting")

            crafting_for_tests.open = function(user, tool, craft_id, notice)
                mobile.message(user, "opened " .. tostring(notice or ""))
            end

            function carpentry_tool.make(serial, user, group, recipe)
                crafting_for_tests.make(user, serial, "carpentry", group, recipe)
            end

            function carpentry_tool.pick(serial, user, kind)
                crafting_for_tests.set_kind(user, kind)
            end

            function carpentry_tool.last(serial, user)
                crafting_for_tests.make_last(user, serial, "carpentry")
            end

            function carpentry_tool.make_in(serial, user, craft_id, group, recipe)
                crafting_for_tests.make(user, serial, craft_id, group, recipe)
            end

            -- The rolls of the script are the test's: the ones queued, then a high one, which is no exceptional item.
            crafting_for_tests.roll = function() return 0.999 end

            function carpentry_tool.set_rolls(serial, ...)
                local rolls = { ... }
                crafting_for_tests.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
            """
        );
        _scripts.Write("common/crafting.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "crafting.lua")));
        _scripts.Write("common/woods.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "woods.lua")));
        _scripts.Write("common/smithy.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "smithy.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Carpentry, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.Musicianship, GainFactor = 1.0, Delay = 1 }
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
    public void TheTool_OnTheGround_IsRefused_InABagOfTheBackpackItOpensTheGump()
    {
        _items.PlaceOnGround(_saw, _aria.Map, _aria.Location);

        Run(_saw);

        Assert.Equal([InBackpack], Told());
        Assert.Empty(Opened());

        var bag = Carry("backpack", 0x0E76, 1);
        _items.MoveToContainer(_saw, bag.Id, new Point2D(5, 5));

        Run(_saw);

        Assert.Empty(_errors);
        Assert.Single(Opened());
    }

    [Fact]
    public void Making_AStool_AtTheMostOfItsSkill_TakesNineBoardsAndGivesIt_AfterTwoStrokes()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 12);

        Make(Stool);

        // The first stroke at once, the second with the result.
        Assert.Equal([1.25], _timers.Timers.Select(timer => timer.Interval.TotalSeconds));
        Assert.Empty(Made("0x0a2b"));

        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([Created], Told());
        Assert.Equal(3, boards.Amount);
        Assert.Single(Made("0x0a2b"));
        Assert.Equal(2, _speech.Sounds.Count(sound => sound.Source == _aria && sound.Sound == Sound));
        Assert.Equal([$"opened {CreatedText}"], Opened());
    }

    [Fact]
    public void Making_BelowTheLeastOfTheSkill_IsRefused_AndTakesNothing()
    {
        Skill(100);
        var boards = Carry("0x1bd7_board", 0x1BD7, 12);

        Make(Stool);

        Assert.Empty(_errors);
        Assert.Equal([NoSkill], Told());
        Assert.Equal(12, boards.Amount);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Making_WithoutEnoughWood_SaysSo_AndTakesNothing()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 8);

        Make(Stool);

        Assert.Empty(_errors);
        Assert.Equal([NoWood], Told());
        Assert.Equal(8, boards.Amount);
        Assert.Empty(_timers.Timers);
    }

    [Theory]
    [InlineData(0.49, true)]
    [InlineData(0.51, false)]
    public void Making_AtTheLeastOfTheSkill_WorksHalfOfTheTimes(double roll, bool made)
    {
        Skill(110);
        Carry("0x1bd7_board", 0x1BD7, 9);
        _random.Doubles(roll);

        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal(made ? 1 : 0, Made("0x0a2b").Count);
        Assert.Equal([made ? Created : FailedAndLost], Told());
    }

    [Fact]
    public void AFailure_TakesHalfOfEachResource_RoundedDown()
    {
        Skill(110);
        var boards = Carry("0x1bd7_board", 0x1BD7, 12);
        _random.Doubles(0.9);

        Make(Stool);
        Fire(1.25);

        // Half of 9 is 4.
        Assert.Empty(_errors);
        Assert.Equal(8, boards.Amount);
        Assert.Empty(Made("0x0a2b"));
        Assert.Equal([$"opened {FailedText}"], Opened());
    }

    [Fact]
    public void TheBoards_AreCountedAndTakenAcrossStacks_OneOfThemInABag()
    {
        var bag = Carry("backpack", 0x0E76, 1);
        var loose = Carry("0x1bd7_board", 0x1BD7, 5);
        var bagged = Carry("0x1bda_board", 0x1BDA, 8);
        _items.MoveToContainer(bagged, bag.Id, new Point2D(5, 5));

        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Single(Made("0x0a2b"));
        Assert.Equal(4, Left(loose) + Left(bagged));
    }

    [Fact]
    public async Task ABoardPileOnTheCursor_OrInTheBankBox_DoesNotCount()
    {
        var held = Carry("0x1bd7_board", 0x1BD7, 9);
        var banked = Carry("0x1bd7_board", 0x1BD7, 9);
        _items.MoveToContainer(banked, _bank.Id, new Point2D(5, 5));
        var holder = _fixture.Sessions.GetAll().First(session => session.CharacterId == _aria.Id);
        await _fixture.Network.ExecuteOnLoopAsync(() => holder.Set(ItemSessionKeys.Held, new HeldItem(held.Id)));

        Make(Stool);

        Assert.Empty(_errors);
        Assert.Equal([NoWood], Told());
        Assert.Equal((9, 9), (held.Amount, banked.Amount));
    }

    [Fact]
    public void AKindOfWood_AsksForItsCarpentry_IsTaken_AndColoursTheItem()
    {
        Skill(649);
        var oak = Carry("oak_board", 0x1BD7, 9);
        oak.Hue = new Hue(0x7DA);
        var plain = Carry("0x1bd7_board", 0x1BD7, 9);

        Pick("oak");

        Assert.Equal([StrangeWood], Told());

        Skill(650);
        Pick("oak");
        // Oak at 65 is no harder for a stool, which asks for 11 to 36.
        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([StrangeWood, Created], Told());
        Assert.Equal((0, 9), (Left(oak), plain.Amount));
        Assert.Equal(new Hue(0x7DA), Assert.Single(Made("0x0a2b")).Hue);
    }

    [Fact]
    public void TheLute_AsksForMusicianship_AndTakesCloth_TryingBothSkills()
    {
        Skill(934);
        Musicianship(400);
        var wood = Carry("0x1bd7_board", 0x1BD7, 25);
        var cloth = Carry("0x175d_cloth", 0x175D, 10);

        Make(Lute);

        Assert.Equal([NoSkill], Told());

        Musicianship(500);
        _random.Doubles(0.0);
        Make(Lute);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NoSkill, Created], Told());
        Assert.Equal((0, 0), (Left(wood), Left(cloth)));
        Assert.Single(Made("0x0eb3_lute"));
        // Musicianship at 50 between 45 and 70 was tried, its try and the chance of a gain; carpentry at its most rolls nothing.
        Assert.Equal(2, _random.Rolls);
    }

    [Fact]
    public void TheLute_WithoutCloth_SaysSo()
    {
        Skill(934);
        Musicianship(500);
        Carry("0x1bd7_board", 0x1BD7, 25);

        Make(Lute);

        Assert.Equal([NoCloth], Told());
    }

    [Fact]
    public void WhileMaking_AnotherAttemptIsRefused()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 18);

        Make(Stool);
        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([Busy, Created], Told());
        Assert.Single(Made("0x0a2b"));
        Assert.Equal(9, boards.Amount);
    }

    [Fact]
    public void TheBoardsMovedAwayBeforeTheSecondStroke_MakeNothing()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 9);

        Make(Stool);
        _items.MoveToContainer(boards, _bank.Id, new Point2D(5, 5));
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([NoWood], Told());
        Assert.Equal(9, boards.Amount);
        Assert.Empty(Made("0x0a2b"));

        // Free to try again.
        _items.MoveToContainer(boards, _backpack.Id, new Point2D(5, 5));
        Make(Stool);
        Fire(1.25);
        Assert.Single(Made("0x0a2b"));
    }

    [Fact]
    public void AFullBackpack_TakesTheBoards_AndPutsTheItemAtTheFeet()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 9);
        _capacity.HasRoomResult = false;

        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal(0, Left(boards));
        var stool = Assert.Single(Made("0x0a2b"));
        Assert.True(_items.IsLyingOnGround(stool));
        Assert.Equal(_aria.Location, stool.GroundLocation);
    }

    [Fact]
    public void BoardsTheInventoryRefusesToGive_MakeNothing_NotEvenOnTheGround()
    {
        // A pending reservation of the inventory refuses every take and every give.
        var boards = Carry("0x1bd7_board", 0x1BD7, 9);

        Make(Stool);
        _guard.Allowed = false;
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal(9, boards.Amount);
        Assert.Empty(Made("0x0a2b"));
        Assert.DoesNotContain(Created, Told());
    }

    [Fact]
    public void WithNoSerialLeftForTheItem_NothingIsTaken_AndTheItemIsNotSaidToBeMade()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 9);
        _capacity.HasRoomResult = false;
        _serials.Serials.Clear();

        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal(9, boards.Amount);
        Assert.DoesNotContain(Created, Told());
    }

    [Fact]
    public void AnItemOfAKindOfWood_IsColouredAlone_NotTheStackOfItsKindAlreadyCarried()
    {
        Skill(650);
        var plainStaves = Carry("0x1eb1_barrel_staves", 0x1EB1, 3);
        var oak = Carry("oak_board", 0x1BD7, 5);
        oak.Hue = new Hue(0x7DA);
        Pick("oak");

        Make(Staves);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal((3, new Hue(0)), (plainStaves.Amount, plainStaves.Hue));
        var made = Assert.Single(Made("0x1eb1_barrel_staves"));
        Assert.Equal((1, new Hue(0x7DA)), (made.Amount, made.Hue));
        Assert.Equal(_backpack.Id, made.ContainerId);
    }

    [Fact]
    public void AFailure_OfARecipeOfOneUnit_StillTakesIt()
    {
        Skill(0);
        var boards = Carry("0x1bd7_board", 0x1BD7, 1);
        _random.Doubles(0.9);

        Make(Peg);
        Fire(1.25);

        // A failure that took nothing would be a free try of the skill.
        Assert.Empty(_errors);
        Assert.Equal(0, Left(boards));
        Assert.Empty(Made("0x14f0_peg"));
    }

    [Fact]
    public async Task BoardsInABagHeldOnTheCursor_DoNotCount()
    {
        var bag = Carry("backpack", 0x0E76, 1);
        var boards = Carry("0x1bd7_board", 0x1BD7, 9);
        _items.MoveToContainer(boards, bag.Id, new Point2D(5, 5));
        var holder = _fixture.Sessions.GetAll().First(session => session.CharacterId == _aria.Id);
        await _fixture.Network.ExecuteOnLoopAsync(() => holder.Set(ItemSessionKeys.Held, new HeldItem(bag.Id)));

        Make(Stool);

        Assert.Empty(_errors);
        Assert.Equal([NoWood], Told());
        Assert.Equal(9, boards.Amount);
    }

    [Fact]
    public void AnAttemptWhoseSecondStrokeNeverCame_FreesThePlayerAfterAWhile()
    {
        // The stroke's timer is lost when the gump script that started it is reloaded.
        Carry("0x1bd7_board", 0x1BD7, 18);

        Make(Stool);
        Make(Stool);
        _time.Now += TimeSpan.FromSeconds(10);
        Make(Stool);

        Assert.Empty(_errors);
        Assert.Equal([Busy], Told());
        Assert.Equal(2, _timers.Timers.Count);
    }

    [Fact]
    public void AnAttemptThatEndsBecauseTheToolLeftTheBackpack_FreesThePlayer()
    {
        Carry("0x1bd7_board", 0x1BD7, 9);

        Make(Stool);
        _items.PlaceOnGround(_saw, _aria.Map, _aria.Location);
        Fire(1.25);
        _items.MoveToContainer(_saw, _backpack.Id, new Point2D(10, 10));
        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Single(Made("0x0a2b"));
    }

    [Theory]
    // At the most of the stool the chance is sure, so an exceptional one comes four times in ten.
    [InlineData(0.39, true)]
    [InlineData(0.41, false)]
    public void ASuccess_IsExceptional_AsOftenAsTheChanceMinusSixTenths(double roll, bool exceptional)
    {
        Carry("0x1bd7_board", 0x1BD7, 9);
        Rolls(roll);

        Make(Stool);
        Fire(1.25);

        var stool = Assert.Single(Made("0x0a2b"));
        Assert.Empty(_errors);
        Assert.Equal([exceptional ? Exceptional : Created], Told());
        Assert.Equal(exceptional, stool.TryGetProp<int>("quality", out var quality) && quality == 2);
        // Below 100 no maker's mark.
        Assert.False(stool.TryGetProp<string>("crafter_name", out _));
    }

    [Fact]
    public void AnExceptionalItem_MadeAtOneHundred_BearsTheMakersMark()
    {
        Skill(1000);
        Carry("0x1bd7_board", 0x1BD7, 9);
        Rolls(0.0);

        Make(Stool);
        Fire(1.25);

        var stool = Assert.Single(Made("0x0a2b"));
        Assert.Empty(_errors);
        Assert.Equal([Marked], Told());
        Assert.True(stool.TryGetProp<long>("crafter_id", out var crafter));
        Assert.Equal(Aria, crafter);
        Assert.True(stool.TryGetProp<string>("crafter_name", out var name));
        Assert.Equal(_aria.Name, name);
    }

    [Fact]
    public void EveryAttempt_UsesTheToolOnce_AndTheLastUseBreaksIt()
    {
        _saw.SetProp("uses_remaining", 2L);
        Carry("0x1bd7_board", 0x1BD7, 30);
        Skill(110);

        // A failure uses it too.
        _random.Doubles(0.9);
        Make(Stool);
        Fire(1.25);

        Assert.True(_saw.TryGetProp<int>("uses_remaining", out var left));
        Assert.Equal(1, left);

        _random.Doubles(0.0);
        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([FailedAndLost, Created, WornOut], Told());
        Assert.False(_items.TryGet(_saw.Id, out _));
    }

    [Fact]
    public void ARefusedAttempt_DoesNotUseTheTool()
    {
        Skill(100);
        Carry("0x1bd7_board", 0x1BD7, 9);

        Make(Stool);

        Assert.True(_saw.TryGetProp<int>("uses_remaining", out var left));
        Assert.Equal(50, left);
    }

    [Theory]
    [InlineData(0.0, 25)]
    [InlineData(0.999, 75)]
    public void ANewTool_DrawsItsUses_TheFirstTimeItIsUsed(double roll, int uses)
    {
        _saw.RemoveProp("uses_remaining");
        Rolls(roll);

        Run(_saw);

        Assert.Empty(_errors);
        Assert.True(_saw.TryGetProp<int>("uses_remaining", out var left));
        Assert.Equal(uses, left);
    }

    [Fact]
    public void MakeLast_SaysThereIsNothingYet_ThenMakesTheLastRecipeAgain()
    {
        Carry("0x1bd7_board", 0x1BD7, 18);

        Call("last", Aria);

        Assert.Equal([NothingYet], Told());
        Assert.Empty(_timers.Timers);

        Make(Stool);
        Fire(1.25);
        Call("last", Aria);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal(2, Made("0x0a2b").Count);
    }

    [Fact]
    public void AnExceptionalRoll_NeverUpgradesAStackTheItemJoins()
    {
        Skill(250);
        var staves = Carry("0x1eb1_barrel_staves", 0x1EB1, 3);
        Carry("0x1bd7_board", 0x1BD7, 5);
        Rolls(0.0);

        Make(Staves);
        Fire(1.25);

        // The staff joins the three already carried; none of them is exceptional for it.
        Assert.Empty(_errors);
        Assert.Equal(4, staves.Amount);
        Assert.False(staves.TryGetProp<long>("quality", out _));
        Assert.Equal([Created], Told());
    }

    [Fact]
    public void AToolWhoseUsesAreNoNumber_DrawsThemAgain()
    {
        _saw.SetProp("uses_remaining", "many");
        Carry("0x1bd7_board", 0x1BD7, 9);

        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.True(_saw.TryGetProp<int>("uses_remaining", out var left));
        Assert.Equal(74, left);
    }

    [Fact]
    public void AnAttemptWhoseItemCannotBeMade_StillUsesTheTool()
    {
        Carry("0x1bd7_board", 0x1BD7, 9);
        _capacity.HasRoomResult = false;
        _serials.Serials.Clear();

        Make(Stool);
        Fire(1.25);

        // The skill was tried: the tool was used.
        Assert.True(_saw.TryGetProp<int>("uses_remaining", out var left));
        Assert.Equal(49, left);
    }

    [Fact]
    public void AToolThatBreaksOnAFailure_OpensNoGump()
    {
        _saw.SetProp("uses_remaining", 1L);
        Skill(110);
        Carry("0x1bd7_board", 0x1BD7, 9);
        _random.Doubles(0.9);

        Make(Stool);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal([FailedAndLost, WornOut], Told());
        Assert.False(_items.TryGet(_saw.Id, out _));
        Assert.Empty(Opened());
    }

    [Fact]
    public void MakeLast_RemembersEachCraftApart()
    {
        Carry("0x1bd7_board", 0x1BD7, 30);

        Make(Stool);
        Fire(1.25);
        Call("make_in", Aria, "woodwork", 1, 1);
        Fire(1.25);
        Call("last", Aria);
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Equal(2, Made("0x0a2b").Count);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private const string CreatedText = "You create the item.";
    private const string FailedText = "You failed to create the item, and some of your materials are lost.";

    private void Skill(int tenths)
    {
        SetSkill(SkillType.Carpentry, tenths);
    }

    private void Musicianship(int tenths)
    {
        SetSkill(SkillType.Musicianship, tenths);
    }

    // The skill service reads the mobile, the mobile module the state service: both hold the same.
    private void SetSkill(SkillType skill, int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == skill);
        _aria.Skills.Add(new MobileSkill { Skill = skill, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == skill);
        _state.Skills.Add(new MobileSkill { Skill = skill, Base = tenths });
    }

    private void Make(int recipe)
    {
        Call("make", Aria, 1, recipe);
    }

    private void Rolls(params double[] rolls)
    {
        Call("set_rolls", rolls.Cast<object?>().ToArray());
    }

    private void Pick(string kind)
    {
        Call("pick", Aria, kind);
    }

    private void Call(string function, params object?[] args)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_saw, function, args);

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

    // What is left of a stack: 0 once it is gone.
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
