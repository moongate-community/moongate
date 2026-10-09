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
///     Runs the help gumps shipped in
///     <c>
///         moongate_root
///     </c>
///     (help_menu and help_page_kind, their templates and scripts) with
///     the real Lua engine: the player flow of Call a game master.
/// </summary>
public sealed class HelpPageKindGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long Other = 9;

    // The buttons answer in the order of the XML.
    private const int CallButton = 4;
    private const int QuestionButton = 1;
    private const int BugButton = 2;
    private const int SuggestionButton = 3;
    private const int HarassmentButton = 4;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingCombatService _combat = new();
    private readonly RecordingCommandSystemService _commands = new();
    private readonly HookedPromptService _prompts = new();
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
        foreach (var script in new[] { "help_menu", "help_page_kind" })
        {
            _scripts.Write(
                $"gumps/{script}.lua",
                await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", $"{script}.lua"))
            );
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
        _container.RegisterInstance<IPromptService>(_prompts);
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
        _container.AddScriptModule<PromptModule>();
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
    public void CallAGameMaster_OpensTheKindGump()
    {
        Press("help_menu", Player, CallButton);

        Assert.Equal("help_page_kind", _gumps.Opened[^1].Gump.Id);
    }

    [Fact]
    public void ABug_AsksForALine_ThenSendsTheRequest_AndTellsTheStaff()
    {
        _prompts.Answer = "The door is stuck";

        Press("help_page_kind", Player, BugButton);

        Assert.Equal(
            ["Type what you need in the journal line.", "Your request was sent to the game masters."],
            Told(Player)
        );
        var page = Assert.Single(_helpPages.Service.Pages);
        Assert.Equal(
            (HelpPageKindType.Bug, "The door is stuck", HelpPageStatusType.Open),
            (page.Kind, page.Text, page.Status)
        );
        Assert.Equal(["Player asks for help (Bug): The door is stuck"], Told(Staff));
        Assert.Empty(_errors);
    }

    [Theory,
     InlineData(QuestionButton, HelpPageKindType.Question),
     InlineData(BugButton, HelpPageKindType.Bug),
     InlineData(SuggestionButton, HelpPageKindType.Suggestion),
     InlineData(HarassmentButton, HelpPageKindType.Harassment)]
    public void EachButton_SendsItsKind(int button, HelpPageKindType kind)
    {
        _prompts.Answer = "x";

        Press("help_page_kind", Player, button);

        Assert.Equal(kind, Assert.Single(_helpPages.Service.Pages).Kind);
    }

    [Fact]
    public void EscapeOnThePrompt_SendsNothing()
    {
        _prompts.Answer = null;

        Press("help_page_kind", Player, BugButton);

        Assert.Equal(["Type what you need in the journal line.", "Nothing was sent."], Told(Player));
        Assert.Empty(_helpPages.Service.Pages);
    }

    [Fact]
    public void WithAnActivePage_ItIsRefusedBeforeThePrompt()
    {
        _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "first");
        _prompts.Answer = "second";

        Press("help_page_kind", Player, BugButton);

        Assert.Equal(["You already asked for help: wait for an answer."], Told(Player));
        Assert.Equal(0, _prompts.Begun);
        Assert.Single(_helpPages.Service.Pages);
    }

    [Fact]
    public void InsideThePause_ItIsRefusedWithTheSeconds()
    {
        var first = _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "first").Page!;
        _helpPages.Service.Close(first.Id, "Gino");
        _prompts.Answer = "second";

        Press("help_page_kind", Player, BugButton);

        Assert.Equal(["Wait 60 seconds before asking again."], Told(Player));
        Assert.Equal(0, _prompts.Begun);
    }

    [Fact]
    public void APlayerInJail_CanCall()
    {
        _jail.SentenceList.Add(
            new()
            {
                Id = new Serial((uint)Player), Name = "Gino", Cell = 1, Days = 3, JailedBy = "Giachi",
                ReleaseAt = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeMilliseconds()
            }
        );
        _prompts.Answer = "Help, I am in jail";

        Press("help_page_kind", Player, QuestionButton);

        Assert.Single(_helpPages.Service.Pages);
    }

    [Fact]
    public void ARequestMadeMeanwhile_IsRefusedAtTheEnd_AndOnlyOnePageExists()
    {
        _prompts.Answer = "second";
        _prompts.BeforeAnswer = () => _helpPages.Service.Create(Mobile(Player), HelpPageKindType.Question, "first");

        Press("help_page_kind", Player, BugButton);

        Assert.Equal("You already asked for help: wait for an answer.", Told(Player)[^1]);
        Assert.Equal("first", Assert.Single(_helpPages.Service.Pages).Text);
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

    private sealed class HookedPromptService : IPromptService
    {
        public string? Answer { get; set; }

        public Action? BeforeAnswer { get; set; }

        public int Begun { get; private set; }

        public void Begin(GameSession session, Action<GameSession, string?> callback)
        {
            Begun++;
            BeforeAnswer?.Invoke();
            callback(session, Answer);
        }

        public void Cancel(GameSession session)
        {
        }

        public bool TryComplete(GameSession session, int promptId, string? text)
        {
            return false;
        }

        public void OnSessionClosed(GameSession session)
        {
        }
    }
}
