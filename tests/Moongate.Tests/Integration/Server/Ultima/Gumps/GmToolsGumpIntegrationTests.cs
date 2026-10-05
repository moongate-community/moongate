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
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Lua;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the gmtools gump shipped in <c>moongate_root</c> (templates/gumps/gmtools.xml and scripts/gumps/gmtools.lua)
///     with the real Lua engine: the sidebar, the weather panel and what its buttons do.
/// </summary>
public sealed class GmToolsGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubWeatherService _weather = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private LuaScriptEngineService _engine = null!;
    private GumpModule _module = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(Staff);
        await _fixture.AddAsync(Player);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));

        // The weather is the one of a player: a mobile with an account.
        foreach (var serial in new[] { Staff, Player })
        {
            Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)serial), out var mobile));
            mobile.AccountId = new Serial((uint)(0x40 + serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write("gumps/gmtools.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "gmtools.lua")));
        var templates = (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
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
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<IWeatherService>(_weather);
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.AddScriptModule<LogModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<GumpModule>();
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>((evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );

        _engine = new(options, _container.Resolve<IScriptModuleRegistry>(), _container, _loop, _timers, new EventBusAdapter(_container));
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
        // The sidebar button, then None, Rain, Snow and Storm.
        Assert.Equal([1, 2, 3, 4, 5], built.Buttons.Order());
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
    [InlineData(2, WeatherKindType.None, "none")]
    [InlineData(3, WeatherKindType.Rain, "rain")]
    [InlineData(4, WeatherKindType.Snow, "snow")]
    [InlineData(5, WeatherKindType.Storm, "storm")]
    public void AKindButton_ForcesItOnTheProfile_TellsTheGameMaster_AndShowsTheWeatherAgain(int button, WeatherKindType kind, string name)
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
    public void APlayer_SeesNoTool()
    {
        var built = Open(Player, "weather");

        Assert.DoesNotContain("Weather", built.Strings);
        Assert.Empty(built.Buttons);
        Assert.Empty(_errors);
    }

    // The account may lose its rank while the gump is open.
    [Fact]
    public async Task AKindClickedByOneWhoIsNoLongerStaff_ForcesNothing()
    {
        Open(Staff, "weather");
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, 5);

        Assert.Empty(_weather.Forced);
        Assert.Empty(_speech.Told);
        Assert.Empty(_errors);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
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

    private void Answer(int gump, int button)
    {
        // As the loop does: what the script posts runs after the script, not inside it.
        _loop.DeferTryPost = true;
        _gumps.Opened[gump].Gump.OnResponse(
            _session,
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
}
