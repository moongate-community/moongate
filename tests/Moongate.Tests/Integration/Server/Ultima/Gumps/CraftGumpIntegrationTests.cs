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
namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     The shipped crafting gump (<c>templates/gumps/craft_menu.xml</c>, <c>scripts/gumps/craft_menu.lua</c>), with the
///     real Lua engine, the shipped crafting engine and the craft service.
/// </summary>
public sealed class CraftGumpIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int Stool = 1;
    private const int Lute = 2;

    private const int InBackpack = 1062334;
    private const int Created = 1044154;
    private const int FailedAndLost = 1044043;
    private const int NoSkill = 1044153;
    private const int NoWood = 1044351;
    private const int NoCloth = 1044287;
    private const int StrangeWood = 1072652;
    private const int Busy = 500119;
    private const int Sound = 0x023D;

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
    private readonly ItemService _items;
    private readonly RecordingGumpService _gumps = new();

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "0x1034_saw", ItemId = new Serial(0x1034), ScriptId = "carpentry_tool" },
            new ItemTemplate { Id = "0x1bd7_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "0x1bda_board", ItemId = new Serial(0x1BDA), Stackable = true },
            new ItemTemplate { Id = "oak_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "0x175d_cloth", ItemId = new Serial(0x175D), Stackable = true },
            new ItemTemplate { Id = "0x0a2b", ItemId = new Serial(0x0A2B) },
            new ItemTemplate { Id = "0x0eb3_lute", ItemId = new Serial(0x0EB3) }
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
                                }
                            ]
                        },
                        new()
                        {
                            Name = "Musical items",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Lute", Item = "0x0eb3_lute", SkillMin = 68.4, SkillMax = 93.4,
                                    Resources = [new() { Resource = "wood", Amount = 25 }, new() { Resource = "cloth", Amount = 10 }],
                                    Skills = [new() { Skill = "musicianship", Min = 45, Max = 70 }]
                                }
                            ]
                        },
                        new()
                        {
                            Name = "Many",
                            Recipe = Enumerable.Range(1, 12)
                                .Select(index => new CraftRecipe
                                    {
                                        Name = $"Crate {index}", Item = "0x0a2b", SkillMin = 0, SkillMax = 10,
                                        Resources = [new() { Resource = "wood", Amount = 1 }]
                                    }
                                )
                                .ToList()
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

    public CraftGumpIntegrationTests()
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
        _items.Add([_backpack, _bank, _saw]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // No exceptional item and the most uses unless a test says otherwise: the rolls are the test's.
        _scripts.Write(
            "items/carpentry_tool.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "carpentry_tool.lua")) +
            "\nrequire(\"common.crafting\").roll = function() return 0.999 end\n"
        );
        _scripts.Write("gumps/craft_menu.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "craft_menu.lua")));
        var gumpTemplates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        GumpScriptService? gumpScripts = null;
        _scripts.Write("common/crafting.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "crafting.lua")));
        _scripts.Write("common/woods.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "woods.lua")));
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
        _container.AddScriptModule<GumpModule>();
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
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
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
    }

    [Fact]
    public void TheTool_OpensTheGump_WithTheGroupsOfTheCraft_AndTheRecipesOfTheFirst()
    {
        var built = Use();

        Assert.Empty(_errors);
        Assert.Contains("CARPENTRY", built.Strings);
        Assert.Contains("Chairs", built.Strings);
        Assert.Contains("Musical items", built.Strings);
        Assert.Contains("Stool", built.Strings);
        Assert.DoesNotContain("Lute", built.Strings);
    }

    [Fact]
    public void AGroup_ShowsItsRecipes()
    {
        var built = Use();

        Click(built, "Musical items");

        var musical = Last();
        Assert.Empty(_errors);
        Assert.Contains("Lute", musical.Strings);
        Assert.DoesNotContain("Stool", musical.Strings);
    }

    [Fact]
    public void ALongGroup_IsPagedByTen()
    {
        var built = Use();

        Click(built, "Many");

        var many = Last();
        Assert.Empty(_errors);
        Assert.Contains("Crate 1", many.Strings);
        Assert.Contains("Crate 12", many.Strings);
        // The second page starts at the eleventh.
        Assert.True(many.Layout.IndexOf("{ page 2 }", StringComparison.Ordinal) <
                    many.Layout.IndexOf(Cropped(many, "Crate 11"), StringComparison.Ordinal));
        Assert.True(many.Layout.IndexOf("{ page 2 }", StringComparison.Ordinal) >
                    many.Layout.IndexOf(Cropped(many, "Crate 10"), StringComparison.Ordinal));
    }

    [Fact]
    public void TheInfoPage_ShowsTheGraphicTheResourcesTheSkillsAndTheChance()
    {
        Skill(235);
        Carry("0x1bd7_board", 0x1BD7, 12);
        var built = Use();

        Click(built, "Stool", rightmost: true);

        var info = Last();
        Assert.Empty(_errors);
        // Halfway from 11 to 36: three chances in four.
        Assert.Contains("Chance: 75%", info.Strings);
        Assert.Contains("9 wood (12)", info.Strings);
        Assert.Contains("carpentry 11.0 - 36.0", info.Strings);
        Assert.Contains("{ tilepic", info.Layout);
        Assert.Contains("2603", info.Layout);
    }

    [Fact]
    public void TheMakeButton_StartsAnAttempt()
    {
        Carry("0x1bd7_board", 0x1BD7, 9);
        var built = Use();

        Click(built, "Stool");

        Assert.Empty(_errors);
        Assert.Contains(_timers.Timers, timer => Math.Abs(timer.Interval.TotalSeconds - 1.25) < 0.001);
    }

    [Fact]
    public void TheWoodLine_ShowsTheKindAndItsCount_AndChangingItPicksAKindTheSkillAllows()
    {
        Skill(650);
        Carry("oak_board", 0x1BD7, 9);
        var built = Use();

        Assert.Contains("Wood: plain (0)", built.Strings);

        Click(built, "Change");
        Click(Last(), "oak (9)");

        Assert.Empty(_errors);
        Assert.Contains("Wood: oak (9)", Last().Strings);

        Click(Last(), "Change");
        Click(Last(), "yew (0)");

        Assert.Contains("Wood: oak (9)", Last().Strings);
        Assert.Contains(StrangeWood, Told());
    }

    [Fact]
    public void TheNotice_IsShown()
    {
        Carry("0x1bd7_board", 0x1BD7, 9);
        var built = Use();

        Click(built, "Stool");
        Fire(1.25);

        Assert.Empty(_errors);
        Assert.Contains("You create the item.", Last().Strings);
    }

    [Fact]
    public void MakeLast_MakesTheLastRecipeAgain_OrSaysThereIsNoneYet()
    {
        var boards = Carry("0x1bd7_board", 0x1BD7, 18);
        var built = Use();

        Click(built, "Make last");

        Assert.Contains(1044165, Told());
        Assert.Empty(_timers.Timers);

        Click(Last(), "Stool");
        Fire(1.25);
        Click(Last(), "Make last");
        Fire(1.25);

        // Two stools: 18 boards less 9 twice.
        Assert.Empty(_errors);
        Assert.Equal(0, _items.TryGet(boards.Id, out var left) ? left.Amount : 0);
        Assert.Equal(2, Told().Count(cliloc => cliloc == 1044154));
    }

    [Fact]
    public void Exit_ClosesWithoutAnError()
    {
        var built = Use();
        var count = _gumps.Opened.Count;

        Click(built, "Exit");

        Assert.Empty(_errors);
        Assert.Equal(count, _gumps.Opened.Count);
    }

    [Fact]
    public void AButton_OfAToolNoLongerInTheBackpack_DoesNothing()
    {
        Carry("0x1bd7_board", 0x1BD7, 9);
        var built = Use();
        _items.PlaceOnGround(_saw, _aria.Map, _aria.Location);

        Click(built, "Stool");

        Assert.Empty(_errors);
        Assert.Empty(_timers.Timers);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skill(int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == SkillType.Carpentry);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Carpentry, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == SkillType.Carpentry);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Carpentry, Base = tenths });
    }

    // Double clicks the saw and gives the gump it opened.
    private GumpBuildResult Use()
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_saw, "on_use", Aria);
        Drain();

        return Last();
    }

    private GumpBuildResult Last()
    {
        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    // Presses the button on the row of a text: the nearest one before it, or the rightmost one after it.
    private void Click(GumpBuildResult built, string text, bool rightmost = false)
    {
        var index = built.Strings.ToList().IndexOf(text);
        Assert.True(index >= 0, $"The gump does not show {text}.");
        var label = System.Text.RegularExpressions.Regex.Match(
            built.Layout,
            $@"\{{ (?:croppedtext|text) (-?\d+) (-?\d+) (?:\d+ \d+ )?\d+ {index} \}}"
        );
        Assert.True(label.Success, $"No label for {text}.");
        var (x, y) = (int.Parse(label.Groups[1].Value), int.Parse(label.Groups[2].Value));
        var buttons = System.Text.RegularExpressions.Regex.Matches(built.Layout, @"\{ button (-?\d+) (-?\d+) \d+ \d+ 1 0 (\d+) \}")
            .Where(match => Math.Abs(int.Parse(match.Groups[2].Value) - y) <= 4)
            .Where(match => rightmost ? int.Parse(match.Groups[1].Value) > x : int.Parse(match.Groups[1].Value) < x)
            .OrderBy(match => int.Parse(match.Groups[1].Value))
            .ToList();
        Assert.NotEmpty(buttons);
        var id = int.Parse(buttons[^1].Groups[3].Value);
        var opened = _gumps.Opened.Last(gump => gump.Gump.Layout.Build().Layout == built.Layout);

        _loop.DeferTryPost = true;
        opened.Gump.OnResponse(
            _fixture.Sessions.GetAll().First(session => session.CharacterId == _aria.Id),
            new GumpResponse { ButtonId = id, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
        );
        Drain();
    }

    private static string Cropped(GumpBuildResult built, string text)
    {
        var index = built.Strings.ToList().IndexOf(text);

        return System.Text.RegularExpressions.Regex.Match(built.Layout, $@"\{{ croppedtext -?\d+ -?\d+ \d+ \d+ \d+ {index} \}}").Value;
    }

    private void Drain()
    {
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
        Drain();
    }

    private ItemEntity Carry(string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([item]);

        return item;
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player == _aria).Select(told => told.Cliloc).ToList();
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
