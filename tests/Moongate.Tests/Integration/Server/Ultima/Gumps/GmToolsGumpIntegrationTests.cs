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
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Server.Ultima.Types.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Schedule;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Lua;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the gmtools gump shipped in
///     <c>
///         moongate_root
///     </c>
///     (templates/gumps/gmtools.xml and
///     scripts/gumps/gmtools.lua)
///     with the real Lua engine: the sidebar, the weather panel and what its buttons do.
/// </summary>
public sealed class GmToolsGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long Administrator = 9;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubWeatherService _weather = new();
    private readonly StubSeasonService _seasons = new();
    private readonly RecordingLightService _light = new();
    private readonly StubClockService _clock = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private GameSession _administrator = null!;
    private ScheduleServices _schedule = null!;
    private LuaScriptEngineService _engine = null!;
    private GumpModule _module = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(Staff);
        await _fixture.AddAsync(Player);
        _administrator = await _fixture.AddAsync(Administrator);
        await _fixture.Network.ExecuteOnLoopAsync(() => _administrator.Set(
                SessionKeys.AccountType,
                AccountType.Administrator
            )
        );
        _schedule = await ScheduleServices.CreateAsync();
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));

        // The weather is the one of a player: a mobile with an account.
        foreach (var serial in new[] { Staff, Player, Administrator })
        {
            Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)serial), out var mobile));
            mobile.AccountId = new Serial((uint)(0x40 + serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write(
            "gumps/gmtools.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "gmtools.lua"))
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
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<IClockService>(_clock);
        _container.RegisterInstance<ILightService>(_light);
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<IWeatherService>(_weather);
        _container.RegisterInstance<ISeasonService>(_seasons);
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.AddScriptModule<LogModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<MobileModule>();
        _container.RegisterInstance<ISeasonalEventService>(_schedule.Events);
        _container.RegisterInstance<IScheduleService>(_schedule.Schedule);
        _container.AddScriptModule<ScheduleModule>();
        _container.AddScriptModule<GumpModule>();
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
    public void TheGump_HasASidebarOfTools_AndThePanelOfTheFirstOne()
    {
        var built = Open(Staff, null);

        Assert.Contains("GM tools", built.Strings);
        Assert.Contains("Weather", built.Strings);
        // The three sidebar buttons, then None, Rain, Snow and Storm.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], built.Buttons.Order());
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheSelectedTool_IsInTheTitleHue_AndTheCaptionsOfTheOthersAreNot()
    {
        var built = Open(Staff, "weather");

        // The label of the sidebar, at x 20 + 35, is written with the hue of the titles (68).
        Assert.Matches(@"\{ croppedtext 55 \d+ \d+ \d+ 68 \d+ \}", built.Layout);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheWeatherPanel_ShowsTheProfileAndTheWeatherWhereTheGameMasterStands()
    {
        var built = Open(Staff, "weather");

        Assert.Contains("Weather here: temperate", built.Strings);
        Assert.Contains("Now: rain, density 40, temperature 12", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AnUnknownTool_IsTheFirstOne()
    {
        var built = Open(Staff, "teleporting");

        Assert.Contains("Weather here: temperate", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheSidebarButton_OpensTheGumpOnThatTool()
    {
        Open(Staff, "weather");

        Answer(0, 1);

        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Contains("Weather here: temperate", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_weather.Forced);
        Assert.Empty(_errors);
    }

    [Theory]
    [InlineData(4, WeatherKindType.None, "none")]
    [InlineData(5, WeatherKindType.Rain, "rain")]
    [InlineData(6, WeatherKindType.Snow, "snow")]
    [InlineData(7, WeatherKindType.Storm, "storm")]
    public void AKindButton_ForcesItOnTheProfile_TellsTheGameMaster_AndShowsTheWeatherAgain(
        int button, WeatherKindType kind, string name
    )
    {
        Open(Staff, "weather");
        _weather.State = new(kind, 60, -3);

        Answer(0, button);

        Assert.Equal([("temperate", kind)], _weather.Forced);
        var (player, text) = Assert.Single(_speech.Told);
        Assert.Equal(
            (new Serial((uint)Staff), $"The weather of temperate is now {name} until the next hour."),
            (player.Id, text)
        );
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Contains($"Now: {name}, density 60, temperature -3", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheSeasonTool_ShowsTheSeasonOfThePlayerAndOfItsMap()
    {
        _seasons.Here = SeasonType.Winter;
        _seasons.MapSeason = SeasonType.Summer;

        var built = Open(Staff, "season");

        Assert.Contains("Season here: winter", built.Strings);
        Assert.Contains("Season of your map: summer", built.Strings);
        // The three sidebar buttons, then spring, summer, fall, winter, desolation and auto.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], built.Buttons.Order());
        Assert.DoesNotContain("Weather here: temperate", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheSidebarButtonOfTheSeason_OpensTheSeasonTool()
    {
        Open(Staff, "weather");

        Answer(0, 2);

        Assert.Contains("Season here: fall", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_seasons.Overrides);
        Assert.Empty(_errors);
    }

    [Theory]
    [InlineData(4, SeasonType.Spring, "spring")]
    [InlineData(5, SeasonType.Summer, "summer")]
    [InlineData(6, SeasonType.Fall, "fall")]
    [InlineData(7, SeasonType.Winter, "winter")]
    [InlineData(8, SeasonType.Desolation, "desolation")]
    public void ASeasonButton_SetsItOnTheMapOfTheGameMaster_TellsIt_AndShowsTheSeasonAgain(
        int button, SeasonType season, string name
    )
    {
        Open(Staff, "season");
        _seasons.Here = season;
        _seasons.MapSeason = season;

        Answer(0, button);

        var (map, set) = Assert.Single(_seasons.Overrides);
        Assert.Equal((MapType.Trammel, (SeasonType?)season), (map, set));
        var (player, text) = Assert.Single(_speech.Told);
        Assert.Equal((new Serial((uint)Staff), $"The season of your map is now {name}."), (player.Id, text));
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Contains($"Season here: {name}", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheAutoButton_GivesTheMapItsOwnSeasonBack()
    {
        Open(Staff, "season");
        _seasons.MapSeason = SeasonType.Summer;

        Answer(0, 9);

        Assert.Equal([(MapType.Trammel, (SeasonType?)null)], _seasons.Overrides);
        Assert.Equal(
            "The season of your map is back to its own: summer.",
            Assert.Single(_speech.Told).Text
        );
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task ASeasonClickedByOneWhoIsNoLongerStaff_ChangesNothing()
    {
        Open(Staff, "season");
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, 7);

        Assert.Empty(_seasons.Overrides);
        Assert.Empty(_speech.Told);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheTimeTool_ShowsTheTimeTheMoonsAndTheLightWhereTheGameMasterStands()
    {
        _clock.Time = new GameTime(7, 5);
        _clock.Moon = MoonPhaseType.FullMoon;

        var built = Open(Staff, "time");

        Assert.Contains("Game time here: 07:05", built.Strings);
        Assert.Contains("Moons: Trammel full moon, Felucca full moon", built.Strings);
        Assert.Contains("Light here: 0, following the time of day", built.Strings);
        // The three sidebar buttons, then the four levels and auto.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], built.Buttons.Order());
        Assert.Empty(_errors);
    }

    [Fact]
    public void WithAGlobalLight_TheTimeToolSaysSo()
    {
        _light.SetOverride(26);

        var built = Open(Staff, "time");

        Assert.Contains("Light here: 26, the same for every player", built.Strings);
        Assert.Empty(_errors);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 12)]
    [InlineData(6, 26)]
    [InlineData(7, 31)]
    public void ALightButton_GivesEveryPlayerThatLevel_TellsTheGameMaster_AndShowsTheLightAgain(int button, int level)
    {
        Open(Staff, "time");

        Answer(0, button);

        Assert.Equal(level, _light.Override);
        Assert.Equal($"The global light is now {level}.", Assert.Single(_speech.Told).Text);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Contains($"Light here: {level}, the same for every player", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheAutoLightButton_GoesBackToTheTimeOfDay()
    {
        _light.SetOverride(26);
        Open(Staff, "time");

        Answer(0, 8);

        Assert.Null(_light.Override);
        Assert.Equal("The global light follows the time of day again.", Assert.Single(_speech.Told).Text);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task ALightClickedByOneWhoIsNoLongerStaff_ChangesNothing()
    {
        Open(Staff, "time");
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, 7);

        Assert.Null(_light.Override);
        Assert.Empty(_speech.Told);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlayer_SeesNoTool()
    {
        var built = Open(Player, "weather");

        Assert.DoesNotContain("Weather", built.Strings);
        Assert.Empty(built.Buttons);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AGameMaster_SeesNoEventsTool_AndAskingForItGivesTheFirstOne()
    {
        var built = Open(Staff, "events");

        Assert.DoesNotContain("Events", built.Strings);
        Assert.DoesNotContain("Seasonal events", built.Strings);
        Assert.Contains("Weather here", string.Join('\n', built.Strings));
        Assert.Empty(_errors);
    }

    [Fact]
    public void AnAdministrator_SeesTheEventsTool_WithEveryEventItsDatesModeAndState()
    {
        var built = Open(Administrator, "events");

        Assert.Contains("Events", built.Strings);
        Assert.Contains("Seasonal events", built.Strings);
        Assert.Contains("Halloween (10-20 to 11-02): auto, on", built.Strings);
        Assert.Contains("Winter (12-20 to 01-06): auto, off", built.Strings);
        // Four tools in the sidebar, then auto, on and off for each of the two events.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], built.Buttons.Order());
        Assert.Empty(_errors);
    }

    [Theory, InlineData(5, "halloween", "auto", true), InlineData(6, "halloween", "on", true),
     InlineData(7, "halloween", "off", false),
     InlineData(9, "winter", "on", true)]
    public void AModeButton_SetsTheModeOfTheEvent_TellsTheAdministrator_AndShowsTheToolAgain(
        int button,
        string id,
        string mode,
        bool active
    )
    {
        Open(Administrator, "events");
        var before = _schedule.Scripts.Calls.Count;
        // Halloween is on at the start of the fixture and Winter is off.
        var changed = active != (id == "halloween");

        Answer(0, button, _administrator);

        var state = _schedule.Events.Get(id)!;
        Assert.Equal((mode, active), (state.Mode, state.Active));
        Assert.Contains(_speech.Told, told => told.Text.EndsWith($"is now {mode}."));
        // The hook of an event that changed its state is queued, not run inside the click.
        Assert.Equal(changed, _schedule.Scripts.Calls.Count > before);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task AModeClickedByOneWhoIsNoLongerAnAdministrator_ChangesNothing()
    {
        Open(Administrator, "events");
        await _fixture.Network.ExecuteOnLoopAsync(() => _administrator.Set(SessionKeys.AccountType, AccountType.GameMaster));

        Answer(0, 7, _administrator);

        Assert.Equal("auto", _schedule.Events.Get("halloween")!.Mode);
        Assert.Empty(_speech.Told);
        Assert.Empty(_errors);
    }

    // The account may lose its rank while the gump is open.
    [Fact]
    public async Task AKindClickedByOneWhoIsNoLongerStaff_ForcesNothing()
    {
        Open(Staff, "weather");
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, 7);

        Assert.Empty(_weather.Forced);
        Assert.Empty(_speech.Told);
        Assert.Empty(_errors);
    }

    private GumpBuildResult Open(long player, string? tool)
    {
        var args = new LuaTable();

        if (tool is not null)
        {
            args["tool"] = tool;
        }

        Assert.True(_module.Open(player, "gmtools", args));

        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    private void Answer(int gump, int button, GameSession? from = null)
    {
        // As the loop does: what the script posts runs after the script, not inside it.
        _loop.DeferTryPost = true;
        _gumps.Opened[gump]
            .Gump.OnResponse(
                from ?? _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
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

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
