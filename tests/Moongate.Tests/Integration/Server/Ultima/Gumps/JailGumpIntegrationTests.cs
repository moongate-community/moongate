using DryIoc;
using Lua;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
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
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the jail gump shipped in <c>moongate_root</c> (templates/gumps/jail_sentence.xml and
///     scripts/gumps/jail_sentence.lua) and the release note script (scripts/items/jail_note.lua) with the real Lua
///     engine: the cells the gump lists and what its buttons do.
/// </summary>
public sealed class JailGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long Target = 9;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubJailService _jail = new();
    private readonly SettableClock _clock = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private LuaScriptEngineService _engine = null!;
    private ScriptEngineOptions _options = null!;
    private ItemService _items = null!;
    private GumpModule _module = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(Staff);
        await _fixture.AddAsync(Player);
        await _fixture.AddAsync(Target);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        _jail.Now = _clock.Now.ToUnixTimeMilliseconds();

        for (var number = 1; number <= 12; number++)
        {
            _jail.CellList.Add(new() { Number = number, Location = new Point3D(5276 + number, 1164, 0) });
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write("gumps/jail_sentence.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "jail_sentence.lua")));
        _scripts.Write("items/jail_note.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "jail_note.lua")));
        var templates = (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        _options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        _items = TestItems.Create(_fixture.Sectors);

        GumpScriptService? gumpScripts = null;
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(new RecordingWorldViewService());
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<IJailService>(_jail);
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.AddScriptModule<LogModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<MobileModule>();
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<JailModule>();
        _container.RegisterScriptEnum<JailResultType>();
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>((evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );

        _engine = new(_options, _container.Resolve<IScriptModuleRegistry>(), _container, _loop, _timers, new EventBusAdapter(_container));
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, _options);
        await gumpScripts.StartAsync();
        _module = _container.Resolve<GumpModule>();
    }

    [Fact]
    public void Open_ShowsTheTargetTheDaysAndEveryCell_FreeAndOccupied()
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 2, secondsLeft: 2 * 86400 + 4 * 3600 + 120));

        var built = Open(Staff);

        Assert.Contains("Jail: Lord Pippo", built.Strings);
        Assert.Contains("1", built.Strings);
        Assert.Contains("Cell 1", built.Strings);
        Assert.Contains("Cell 12", built.Strings);
        Assert.Contains("Gino - 2d 4h left", built.Strings);
        Assert.Contains("free", built.Strings);
        // One button per cell: eleven that jail, one that releases.
        Assert.Equal(12, built.Buttons.Count);
        Assert.Contains("{ textentrylimited ", built.Layout);
        // Ten cells a page.
        Assert.Contains("{ page 2 }", built.Layout);
        Assert.Empty(_errors);
    }

    [Theory,
     InlineData(5 * 3600 + 10 * 60 + 30, "5h 10m left"),
     InlineData(12 * 60 + 59, "12m left"),
     InlineData(20, "1m left")]
    public void Open_ShowsTheTimeLeftInItsLargestUnits(long seconds, string left)
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 1, secondsLeft: seconds));

        Assert.Contains("Gino - " + left, Open(Staff).Strings);
    }

    [Fact]
    public void AFreeCell_WithGoodDays_JailsTheTargetThere_AndTellsTheGameMaster()
    {
        Open(Staff);

        Answer(0, 3, "5");

        var (prisoner, cell, days, by) = Assert.Single(_jail.Jailed);
        Assert.Equal((new Serial((uint)Target), 3, 5, new Serial((uint)Staff)), (prisoner.Id, cell, days, by.Id));
        Assert.Equal("Lord Pippo is in cell 3 for 5 days.", Assert.Single(_speech.Told).Text);
        Assert.Single(_gumps.Opened);
        Assert.Empty(_errors);
    }

    [Theory, InlineData(""), InlineData("abc"), InlineData("0"), InlineData("-3"), InlineData("2.5"), InlineData("999999999999"), InlineData("31")]
    public void AFreeCell_WithBadDays_JailsNobody_SaysWhy_AndOpensTheGumpAgain(string days)
    {
        Open(Staff);

        Answer(0, 1, days);

        Assert.Empty(_jail.Jailed);
        Assert.Equal("Type the days as a whole number from 1 to 30.", Assert.Single(_speech.Told).Text);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AFreeCell_TakenMeanwhile_SaysTaken_AndOpensTheGumpAgainWithTheDaysTyped()
    {
        Open(Staff);
        _jail.Result = JailResultType.CellOccupied;

        Answer(0, 1, "4");

        Assert.Equal("That cell is taken.", Assert.Single(_speech.Told).Text);
        Assert.Contains("4", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Release_PardonsThePrisonerOfTheCell_AndOpensTheGumpAgain()
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 2, secondsLeft: 3600));
        Open(Staff);

        Answer(0, 2, "1");

        Assert.Equal([new Serial((uint)Player)], _jail.Pardoned);
        Assert.Equal("Gino is released.", Assert.Single(_speech.Told).Text);
        Assert.Empty(_jail.Jailed);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ATargetAlreadyInJail_SeesItsOwnCellAsItsOwn_AndCanBeReleasedFromTheTop()
    {
        _jail.SentenceList.Add(Sentence(Target, "Lord Pippo", cell: 2, secondsLeft: 3600));

        var built = Open(Staff);

        Assert.Contains("In cell 2, 1h 0m left", built.Strings);
        Assert.Contains("here now", built.Strings);

        // The first button is the release of the target; the cells follow.
        Answer(0, 1, "1");

        Assert.Equal([new Serial((uint)Target)], _jail.Pardoned);
        Assert.Empty(_errors);
    }

    // Its sentence is over and it is offline: the jail releases it when it logs in.
    [Fact]
    public void ATargetWhoseSentenceIsOver_IsSaidToWaitForItsLogin_NotToHaveAMinuteLeft()
    {
        _jail.SentenceList.Add(Sentence(Target, "Lord Pippo", cell: 2, secondsLeft: 0));

        var built = Open(Staff);

        Assert.Contains("Sentence over: free at its next login", built.Strings);
        Assert.DoesNotContain(built.Strings, text => text.Contains("left"));
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlayer_SeesNoCell()
    {
        var built = Open(Player);

        Assert.DoesNotContain("Cell 1", built.Strings);
        Assert.Empty(built.Buttons);
        Assert.Empty(_errors);
    }

    // The account may lose its rank while the gump is open.
    [Fact]
    public async Task ACellClickedByOneWhoIsNoLongerStaff_DoesNothing()
    {
        Open(Staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, 1, "3");

        Assert.Empty(_jail.Jailed);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task TheNote_OnADoubleClick_ShowsItsText()
    {
        var note = new ItemEntity
        {
            Id = new Serial(0x40000001), TemplateId = "jail_release_note", ItemId = 0x14F0, Amount = 1,
            Props = new() { ["jail.text"] = "Lord Pippo served 3 days in cell 2." }
        };
        note.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([note]);
        var scripts = new ItemScriptService(
            _engine,
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "jail_release_note", ScriptId = "jail_note" })),
            _loop,
            _options
        );
        await scripts.StartAsync();

        scripts.Run(note, "on_use", Staff);

        Assert.Contains("Lord Pippo served 3 days in cell 2.", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private JailSentenceEntity Sentence(long prisoner, string name, int cell, long secondsLeft)
    {
        return new()
        {
            Id = new Serial((uint)prisoner), Name = name, Cell = cell, Days = 3, JailedBy = "Giachi",
            ReleaseAt = _jail.Now + secondsLeft * 1000
        };
    }

    private GumpBuildResult Open(long player)
    {
        var args = new LuaTable();
        args["target"] = Target;
        args["name"] = "Lord Pippo";
        args["days"] = "1";

        Assert.True(_module.Open(player, "jail_sentence", args));

        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    private void Answer(int gump, int button, string days)
    {
        // As the loop does: what the script posts runs after the script, not inside it.
        _loop.DeferTryPost = true;
        _gumps.Opened[gump].Gump.OnResponse(
            _session,
            new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string> { [1] = days } }
        );
        _loop.RunDeferred();
        _loop.DeferTryPost = false;
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
