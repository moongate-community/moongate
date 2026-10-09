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
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Commands;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the help gump shipped in
///     <c>
///         moongate_root
///     </c>
///     (templates/gumps/help_menu.xml and
///     scripts/gumps/help_menu.lua) with the real Lua engine: what its three buttons do.
/// </summary>
public sealed class HelpMenuGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long Other = 9;

    // The buttons answer in the order of the XML: I am stuck, Useful commands, Server rules.
    private const int StuckButton = 1;
    private const int CommandsButton = 2;
    private const int RulesButton = 3;

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
        _scripts.Write(
            "gumps/help_menu.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "help_menu.lua"))
        );
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
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<IDataLoaderService>(_data);
        _container.RegisterInstance(new HelpConfig { StuckWaitSeconds = 5, StuckCooldownMinutes = 10 });
        _container.RegisterInstance(
            TestLocalization.With(
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
    public void Stuck_Accepted_TellsToStandStill_AndMovesAfterTheWait()
    {
        Press(Player, StuckButton);

        Assert.Equal(["Stand still for 5 seconds and you will be taken to Near."], Told(Player));
        Assert.Empty(_teleports.Teleports);

        FireTimers();

        var (mobile, map, location) = Assert.Single(_teleports.Teleports);
        Assert.Equal((new Serial((uint)Player), MapType.Trammel, new Point3D(30, 40, 0)), (mobile.Id, map, location));
        Assert.Equal("You have been taken to Near.", Told(Player)[^1]);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Stuck_TheCharacterMovedDuringTheWait_MovesNobody_AndSpendsNoPause()
    {
        Press(Player, StuckButton);
        MoveTo(Player, 11, 12);
        FireTimers();

        Assert.Empty(_teleports.Teleports);
        Assert.Equal("You moved: you stay where you are.", Told(Player)[^1]);

        Press(Player, StuckButton);

        Assert.StartsWith("Stand still for 5 seconds", Told(Player)[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Stuck_PressedTwice_StartsOneTimerOnly()
    {
        Press(Player, StuckButton);
        Press(Player, StuckButton);

        Assert.Equal("You already asked to be moved: stand still.", Told(Player)[^1]);
        Assert.Single(_timers.Timers);
    }

    [Fact]
    public void Stuck_InJail_IsRefused()
    {
        _jail.SentenceList.Add(Sentence(Player));

        Press(Player, StuckButton);

        Assert.Equal(["You cannot ask to be moved while you are in jail."], Told(Player));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Stuck_Fighting_IsRefused()
    {
        _combat.Attacks.Add((Mobile(Player), Mobile(Other)));

        Press(Player, StuckButton);

        Assert.Equal(["You cannot ask to be moved while you are fighting."], Told(Player));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Stuck_JailedDuringTheWait_MovesNobody()
    {
        Press(Player, StuckButton);
        _jail.SentenceList.Add(Sentence(Player));
        FireTimers();

        Assert.Empty(_teleports.Teleports);
        Assert.Equal("You cannot ask to be moved while you are in jail.", Told(Player)[^1]);
    }

    [Fact]
    public void Stuck_AfterAMove_ARepeatInsideThePauseIsRefusedWithTheMinutesLeft()
    {
        Press(Player, StuckButton);
        FireTimers();

        Press(Player, StuckButton);

        Assert.Equal("You can ask to be moved again in 10 minutes.", Told(Player)[^1]);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Stuck_WhenThePauseIsOver_IsAcceptedAgain()
    {
        Press(Player, StuckButton);
        FireTimers();
        _clock.Advance(TimeSpan.FromMinutes(11));

        Press(Player, StuckButton);

        Assert.StartsWith("Stand still for 5 seconds", Told(Player)[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Stuck_TheStaff_HasNoPause()
    {
        Press(Staff, StuckButton);
        FireTimers();
        Press(Staff, StuckButton);

        Assert.DoesNotContain(
            Told(Staff),
            text => text.StartsWith("You can ask to be moved again", StringComparison.Ordinal)
        );
        Assert.StartsWith("Stand still for 5 seconds", Told(Staff)[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Stuck_ThePlayerLeavesTheWorldDuringTheWait_NothingHappens_AndNoScriptError()
    {
        Press(Player, StuckButton);
        _fixture.Mobiles.LeaveWorld(new Serial((uint)Player));
        FireTimers();

        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Stuck_NoCityAtAll_SaysSo()
    {
        _data.With<StartingCityContent>();

        Press(Player, StuckButton);

        Assert.Equal(["There is no city to take you to."], Told(Player));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Stuck_AGhost_IsAccepted()
    {
        Mobile(Player).Body = 0x192;

        Press(Player, StuckButton);

        Assert.StartsWith("Stand still for 5 seconds", Told(Player)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Rules_SendTheRulesText()
    {
        Press(Player, RulesButton);

        Assert.Equal(["Be kind to the other players, do not cheat and do not use bugs for your own gain."], Told(Player));
    }

    [Fact]
    public void Commands_RunTheHelpCommandAsThePlayer()
    {
        Press(Player, CommandsButton);

        Assert.Equal(("help", CommandSourceType.InGame, _sessions[Player]), Assert.Single(_commands.Executed));
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
    private void Press(long serial, int button)
    {
        Assert.True(_module.Open(serial, "help_menu"));

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
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
