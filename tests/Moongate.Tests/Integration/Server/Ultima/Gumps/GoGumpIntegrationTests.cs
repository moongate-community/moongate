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
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
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
///     Runs the go gump shipped in <c>moongate_root</c> (templates/gumps/go.xml and scripts/gumps/go.lua) with the real
///     Lua engine: the levels it lists and what its buttons do.
/// </summary>
public sealed class GoGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly List<NamedLocation> _places =
    [
        Place(MapType.Felucca, "Towns/Britain", "Bank", 1434, 1699, 2),
        Place(MapType.Felucca, "", "Arena", 1400, 1500, -3),
        Place(MapType.Felucca, "Towns", "Cove", 2275, 1210, 0),
        Place(MapType.Trammel, "Towns", "Haven", 3500, 2570, 14)
    ];

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

        for (var index = 1; index <= 30; index++)
        {
            _places.Add(Place(MapType.Trammel, "Shrines", $"Shrine {index}", 100 + index, 100, 0));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write("gumps/go.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "go.lua")));
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
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ITeleportService>(_teleports);
        _container.RegisterInstance<ILocationService>(
            new LocationService(new StubDataLoaderService().With(_places.ToArray()), _fixture.Sectors)
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
        _container.AddScriptModule<LocationsModule>();
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
    public void AMap_ListsItsCategoriesThenItsPlaces()
    {
        var built = Open(Staff, "Felucca");

        Assert.Contains("Felucca", built.Strings);
        Assert.True(IndexOf(built, "Towns") < IndexOf(built, "Arena"));
        Assert.DoesNotContain("Cove", built.Strings);
        Assert.DoesNotContain("Haven", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void WithoutAPath_TheMapsAreListed_AndThereIsNoWayBack()
    {
        var built = Open(Staff, null);

        Assert.Contains("Felucca", built.Strings);
        Assert.Contains("Trammel", built.Strings);
        // One button per map.
        Assert.Equal(2, built.Buttons.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AnUnknownPath_ListsTheMaps()
    {
        var built = Open(Staff, "Atlantis/Towns");

        Assert.Contains("Trammel", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ACategory_OpensTheGumpOneLevelDown()
    {
        Open(Staff, "Felucca");

        // Back, then Towns, then Arena.
        Answer(0, 2);

        var towns = _gumps.Opened[1].Gump.Layout.Build();
        Assert.Contains("Felucca/Towns", towns.Strings);
        Assert.Contains("Britain", towns.Strings);
        Assert.Contains("Cove", towns.Strings);
        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Back_OpensTheGumpOneLevelUp()
    {
        Open(Staff, "Felucca/Towns");

        Answer(0, 1);

        var felucca = _gumps.Opened[1].Gump.Layout.Build();
        Assert.Contains("Arena", felucca.Strings);
        Answer(1, 1);
        Assert.Contains("Trammel", _gumps.Opened[2].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlace_TakesTheTravellerThere_AndKeepsTheGumpOpen()
    {
        Open(Staff, "Felucca");

        Answer(0, 3);

        var (mobile, map, location) = Assert.Single(_teleports.Teleports);
        Assert.Equal((new Serial((uint)Staff), MapType.Felucca, new Point3D(1400, 1500, -3)), (mobile.Id, map, location));
        Assert.Contains("Arena", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlaceTheWorldRefuses_TellsTheTraveller_AndKeepsTheGumpOpen()
    {
        _teleports.Result = false;
        Open(Staff, "Felucca");

        Answer(0, 3);

        var (player, text) = Assert.Single(_speech.Told);
        Assert.Equal(
            (new Serial((uint)Staff), "You cannot go to Arena: its map is not loaded or the spot is outside it."),
            (player.Id, text)
        );
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlaceTheWorldAccepts_TellsNothing()
    {
        Open(Staff, "Felucca");

        Answer(0, 3);

        Assert.Empty(_speech.Told);
    }

    // A long path or name is cut at the frame instead of running over it.
    [Fact]
    public void TheTexts_AreCutToTheFrame()
    {
        var built = Open(Staff, "Felucca");

        // The heading of the frame is the one text left as it is.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(built.Layout, @"\{ text "));
        // The path, Back, Towns and Arena.
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Count(built.Layout, @"\{ croppedtext "));
    }

    [Fact]
    public void ALongList_IsPaged()
    {
        var built = Open(Staff, "Trammel/Shrines");

        Assert.Contains("{ page 2 }", built.Layout);
        Assert.Contains("Shrine 30", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlayer_SeesNoPlace()
    {
        var built = Open(Player, "Felucca");

        Assert.DoesNotContain("Towns", built.Strings);
        Assert.DoesNotContain("Arena", built.Strings);
        Assert.Empty(built.Buttons);
        Assert.Empty(_errors);
    }

    // The account may lose its rank while the gump is open.
    [Fact]
    public async Task APlaceClickedByOneWhoIsNoLongerStaff_DoesNothing()
    {
        Open(Staff, "Felucca");
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, 3);

        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_errors);
    }

    private GumpBuildResult Open(long player, string? path)
    {
        var args = new LuaTable();

        if (path is not null)
        {
            args["path"] = path;
        }

        Assert.True(_module.Open(player, "go", args));

        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    private void Answer(int gump, int button)
    {
        // As the loop does: what the script posts runs after the script, not inside it.
        _loop.DeferTryPost = true;
        _gumps.Opened[gump]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
        _loop.RunDeferred();
        _loop.DeferTryPost = false;
    }

    private static int IndexOf(GumpBuildResult built, string text)
    {
        var index = built.Strings.ToList().IndexOf(text);
        Assert.True(index >= 0, $"The gump does not show {text}.");

        return index;
    }

    private static NamedLocation Place(MapType map, string category, string name, int x, int y, int z)
    {
        return new() { Map = map, Category = category, Name = name, Location = new Point3D(x, y, z) };
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
