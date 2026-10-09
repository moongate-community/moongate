using Lua;
using DryIoc;
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
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.Help;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Commands;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Help;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the staff gumps of the requests for the game masters shipped in
///     <c>
///         moongate_root
///     </c>
///     (pages and pages_detail,
///     their templates and scripts) with the real Lua engine: the queue, the detail and what its buttons do.
/// </summary>
public sealed class PagesGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long Other = 9;

    // The buttons of pages_detail answer in the order of the XML.
    private const int AnswerButton = 1;
    private const int GoButton = 2;
    private const int TakeButton = 3;
    private const int CloseButton = 4;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingCombatService _combat = new();
    private readonly RecordingCommandSystemService _commands = new();
    private readonly StubJailService _jail = new();
    private readonly SettableClock _clock = new();
    private readonly StubDataLoaderService _data = new();
    private readonly Dictionary<long, GameSession> _sessions = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private BroadcastFixture _fixture = null!;
    private HelpPageServices _helpPages = null!;
    private LuaScriptEngineService _engine = null!;
    private GumpModule _module = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();

        foreach (var serial in new[] { Staff, Player, Other })
        {
            _sessions[serial] = await _fixture.AddAsync(serial);
        }

        await _fixture.Network.ExecuteOnLoopAsync(() => _sessions[Staff].Set(SessionKeys.AccountType, AccountType.GameMaster)
        );
        _data.With(
            City("Far", 3000, 2000, MapType.Trammel),
            City("Near", 30, 40, MapType.Trammel),
            City("Elsewhere", 1, 1, MapType.Felucca)
        );

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        foreach (var script in new[] { "gumps/pages", "gumps/pages_detail", "common/help_pages" })
        {
            _scripts.Write($"{script}.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", $"{script}.lua")));
        }

        var templates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var items = TestItems.Create(_fixture.Sectors);

        GumpScriptService? gumpScripts = null;
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ITeleportService>(_teleports);
        _container.RegisterInstance<ICombatService>(_combat);
        _container.RegisterInstance<ICommandSystemService>(_commands);
        _container.RegisterInstance<IJailService>(_jail);
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<IDataLoaderService>(_data);
        _container.RegisterInstance(new HelpConfig { StuckWaitSeconds = 5, StuckCooldownMinutes = 10 });
        _container.RegisterInstance(
            TestLocalization.With(
                (30206, "What is it about?"),
                (30207, "Question"),
                (30208, "Bug"),
                (30209, "Suggestion"),
                (30210, "Harassment"),
                (30211, "Type what you need in the journal line."),
                (30212, "Your request was sent to the game masters."),
                (30213, "You already asked for help: wait for an answer."),
                (30214, "Wait {0} seconds before asking again."),
                (30215, "Nothing was sent."),
                (30216, "{0} asks for help ({1}): {2}"),
                (30192, "You cannot ask to be moved while you are in jail."),
                (30193, "You cannot ask to be moved while you are fighting."),
                (30194, "You already asked to be moved: stand still."),
                (30195, "You can ask to be moved again in {0} minutes."),
                (30196, "Stand still for {0} seconds and you will be taken to {1}."),
                (30197, "You moved: you stay where you are."),
                (30198, "You have been taken to {0}."),
                (30199, "There is no city to take you to."),
                (30200, "Be kind to the other players, do not cheat and do not use bugs for your own gain.")
            )
        );
        _helpPages = HelpPageServices.Create(_fixture, _speech);
        await _helpPages.Service.StartAsync();
        _container.RegisterInstance<IHelpPageService>(_helpPages.Service);
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.AddScriptModule<LogModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<JailModule>();
        _container.AddScriptModule<CombatModule>();
        _container.AddScriptModule<HelpModule>();
        _container.AddScriptModule<LocalizationModule>();
        _container.AddScriptModule<CommandsModule>();
        _container.RegisterScriptEnum<HelpPageKindType>();
        _container.RegisterScriptEnum<HelpPageStatusType>();
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
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _module = _container.Resolve<GumpModule>();
    }

    [Fact]
    public void Pages_ListsTheActivePagesOldestFirst_WithNameKindAgeAndStatus()
    {
        _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "first");
        Advance(TimeSpan.FromMinutes(3));
        var second = _helpPages.Service.Create(Mobile(Other), HelpPageKindType.Harassment, "second").Page!;
        _helpPages.Service.Take(second.Id, "Gino");

        var built = Open("pages", Staff);

        var rows = built.Strings.Where(text => text.StartsWith('#')).ToList();
        Assert.Equal(["#1 Player, Bug, 3 min, open", "#2 Player, Harassment, now, taken by Gino"], rows);
        Assert.Contains("Help requests", built.Strings);
    }

    [Fact]
    public void Pages_APlayerWhoIsNotStaff_SeesNothing()
    {
        _helpPages.Service.Create(Mobile(Other), HelpPageKindType.Bug, "x");

        var built = Open("pages", Player);

        Assert.DoesNotContain(built.Strings, text => text.StartsWith('#'));
    }

    [Fact]
    public void Pages_AnEmptyQueue_ShowsNothingButTheTitle()
    {
        var built = Open("pages", Staff);

        Assert.DoesNotContain(built.Strings, text => text.StartsWith('#'));
        Assert.Contains("Help requests", built.Strings);
    }

    [Fact]
    public void Pages_AClosedPage_IsNotListed()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "x").Page!;
        _helpPages.Service.Close(page.Id, "Gino");

        Assert.DoesNotContain(Open("pages", Staff).Strings, text => text.StartsWith('#'));
    }

    [Fact]
    public void Pages_MoreThanTenRows_ArePaged()
    {
        for (var index = 0; index < 12; index++)
        {
            var serial = 100 + index;
            _fixture.Mobiles.EnterWorld(
                new MobileEntity { Id = new Serial((uint)serial), Name = $"P{index}", Map = MapType.Trammel }
            );
            _helpPages.Service.Create(Mobile(serial), HelpPageKindType.Question, "x");
        }

        var built = Open("pages", Staff);

        Assert.Equal(12, built.Strings.Count(text => text.StartsWith('#')));
        Assert.Equal(12, built.Buttons.Count);
    }

    [Fact]
    public void ARow_OpensTheDetailOfThatPage()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "The door is stuck").Page!;
        _helpPages.Service.Take(page.Id, "Gino");
        Open("pages", Staff);

        Press("pages", Staff, 1);

        var detail = _gumps.Opened[^1].Gump;
        Assert.Equal("pages_detail", detail.Id);
        var strings = detail.Layout.Build().Strings;
        Assert.Contains("Request 1: Player", strings);
        Assert.Contains("Bug, now, taken by Gino", strings);
        Assert.Contains("Trammel 0, 0, 0", strings);
        Assert.Contains("The door is stuck", strings);
    }

    [Fact]
    public void Detail_Go_TeleportsTheStaffToTheCurrentPositionOfTheOnlinePlayer()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "x").Page!;
        Assert.True(_fixture.Mobiles.MoveTo(Mobile(Player), MapType.Trammel, new Point3D(50, 60, 5)));

        PressDetail(page, GoButton);

        var (mobile, map, location) = Assert.Single(_teleports.Teleports);
        Assert.Equal((new Serial((uint)Staff), MapType.Trammel, new Point3D(50, 60, 5)), (mobile.Id, map, location));
    }

    [Fact]
    public void Detail_Go_ForAnOfflinePlayer_GoesToTheRecordedPosition()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "x").Page!;
        page.X = 7;
        page.Y = 8;
        page.Z = 9;
        _fixture.Mobiles.LeaveWorld(new Serial((uint)Player));

        PressDetail(page, GoButton);

        Assert.Equal(new Point3D(7, 8, 9), Assert.Single(_teleports.Teleports).Location);
    }

    [Fact]
    public void Detail_Take_MarksItTaken_AndTellsTheStaff()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "x").Page!;

        PressDetail(page, TakeButton);

        Assert.Equal((HelpPageStatusType.Taken, "Player"), (page.Status, page.TakenBy));
        Assert.Equal("Request 1 is yours.", Told(Staff)[^1]);
    }

    [Fact]
    public void Detail_Answer_SendsTheTypedText_ClosesThePage_AndOpensTheQueue()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        PressDetail(page, AnswerButton, "  Go north  ");

        Assert.Equal((HelpPageStatusType.Closed, "Go north"), (page.Status, page.Answer));
        Assert.Contains("Game master Player answers: Go north", Told(Player));
        Assert.Equal("Answered request 1.", Told(Staff)[^1]);
        Assert.Equal("pages", _gumps.Opened[^1].Gump.Id);
    }

    [Fact]
    public void Detail_Answer_WithAnEmptyField_DoesNothing_AndSaysSo()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        PressDetail(page, AnswerButton, "   ");

        Assert.True(page.IsActive);
        Assert.Equal("Type an answer first.", Told(Staff)[^1]);
        Assert.Equal("pages_detail", _gumps.Opened[^1].Gump.Id);
    }

    [Fact]
    public void Detail_Close_ClosesWithoutAnswer()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        PressDetail(page, CloseButton);

        Assert.Equal(HelpPageStatusType.Closed, page.Status);
        Assert.Equal("", page.Answer);
        Assert.Equal("Closed request 1.", Told(Staff)[^1]);
    }

    [Fact]
    public void Detail_OfAPageClosedMeanwhile_SaysItIsClosed_AndChangesNothing()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;
        Assert.True(OpenDetail(page));
        _helpPages.Service.Answer(page.Id, "Pina", "first");

        PressOpened(TakeButton);
        PressOpened(AnswerButton, "second");
        PressOpened(CloseButton);

        Assert.Equal(("Pina", "first"), (page.TakenBy, page.Answer));
        Assert.Equal(3, Told(Staff).Count(text => text == "Request 1 is already closed."));
        Assert.Single(Told(Player));
    }

    [Fact]
    public void Detail_ThePlayersTextIsShownEscaped()
    {
        var page = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Bug, "<a href=\"x\">&</a>").Page!;

        Assert.True(OpenDetail(page));

        Assert.Contains("&lt;a href=&quot;x&quot;&gt;&amp;&lt;/a&gt;", _gumps.Opened[^1].Gump.Layout.Build().Strings);
    }

    [Fact]
    public void Detail_ForSomeoneWhoIsNotStaff_DoesNothing()
    {
        var page = _helpPages.Service.Create(Mobile(Other), HelpPageKindType.Bug, "x").Page!;
        _module.Open(Player, "pages_detail", Args(page));

        Press("pages_detail", Player, TakeButton);

        Assert.Equal(HelpPageStatusType.Open, page.Status);
    }

    // The page service and the Lua modules read two clocks in this test.
    private void Advance(TimeSpan elapsed)
    {
        _clock.Advance(elapsed);
        _helpPages.Clock.Advance(elapsed);
    }

    private LuaTable Args(HelpPageEntity page)
    {
        var args = new LuaTable();
        args["page"] = page.Id.Value.ToString();
        args["name"] = page.PlayerName;
        args["kind"] = page.Kind.ToString();
        args["status"] = "open";
        args["age"] = "now";
        args["where"] = "Trammel 0, 0, 0";
        args["text"] = page.Text;
        args["answer"] = "";

        return args;
    }

    private bool OpenDetail(HelpPageEntity page)
    {
        return _module.Open(Staff, "pages_detail", Args(page));
    }

    private void PressDetail(HelpPageEntity page, int button, string? typed = null)
    {
        Assert.True(OpenDetail(page));
        PressOpened(button, typed);
    }

    // Presses a button of the gump that is open now, with the text of the answer field.
    private void PressOpened(int button, string? typed = null)
    {
        _loop.DeferTryPost = true;
        _gumps.Opened[^1]
            .Gump.OnResponse(
                _sessions[Staff],
                new GumpResponse
                {
                    ButtonId = button, Switches = new HashSet<int>(),
                    Texts = typed is null ? new Dictionary<int, string>() : new Dictionary<int, string> { [1] = typed }
                }
            );
        _loop.RunDeferred();
        _loop.DeferTryPost = false;
    }

    private GumpBuildResult Open(string gump, long player)
    {
        Assert.True(_module.Open(player, gump));

        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    private static StartingCityContent City(string town, int x, int y, MapType map)
    {
        return new StartingCityContent { Town = town, Description = "inn", Location = new Point3D(x, y, 0), Map = map };
    }

    private MobileEntity Mobile(long serial)
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)serial), out var mobile));

        return mobile;
    }

    private void MoveTo(long serial, int x, int y)
    {
        Assert.True(_fixture.Mobiles.MoveTo(Mobile(serial), MapType.Trammel, new Point3D(x, y, 0)));
    }

    private static JailSentenceEntity Sentence(long prisoner)
    {
        return new()
        {
            Id = new Serial((uint)prisoner), Name = "Gino", Cell = 1, Days = 3, JailedBy = "Giachi",
            ReleaseAt = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeMilliseconds()
        };
    }

    private List<string> Told(long serial)
    {
        return _speech.Told.Where(told => told.Player.Id.Value == (uint)serial).Select(told => told.Text).ToList();
    }

    // Opens the help gump of the player and presses a button of it, as the client would.
    private void Press(string gump, long serial, int button)
    {
        Assert.True(_module.Open(serial, gump));

        // As the loop does: what the script posts runs after the script, not inside it.
        _loop.DeferTryPost = true;
        _gumps.Opened[^1]
            .Gump.OnResponse(
                _sessions[serial],
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
        _loop.RunDeferred();
        _loop.DeferTryPost = false;
    }

    private void FireTimers()
    {
        foreach (var timer in _timers.Timers.ToList())
        {
            _timers.Fire(timer.Id);
        }

        _loop.RunDeferred();
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

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _helpPages.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
