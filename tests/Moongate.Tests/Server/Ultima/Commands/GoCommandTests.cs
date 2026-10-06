using System.Xml.Linq;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class GoCommandTests : IAsyncDisposable
{
    private const string Usage = "Usage: go [<x>,<y>,<z> [map] | <place>]";

    private readonly RecordingTeleportService _teleports = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly RecordingGumpService _gumps = new();
    private readonly List<NamedLocation> _places = [];

    private SessionFixture? _fixture;

    [Theory,
     InlineData("1496,1628,10"),
     InlineData("1496", "1628", "10"),
     InlineData("1496,", "1628,", "10"),
     InlineData("1496,1628", "10"),
     InlineData("+1496", "+1628", "+10")]
    public async Task WithThreeNumbers_TakesYouThereOnYourOwnMap(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        var (mobile, map, location) = Assert.Single(_teleports.Teleports);
        Assert.Equal((new Serial(2), MapType.Trammel, new Point3D(1496, 1628, 10)), (mobile.Id, map, location));
        Assert.Empty(context.Output);
    }

    [Theory, InlineData("Felucca"), InlineData("felucca"), InlineData("FELUCCA")]
    public async Task WithAMap_TakesYouToThatMap(string map)
    {
        await RunAsync("1,1,1", map);

        Assert.Equal(
            (MapType.Felucca, new Point3D(1, 1, 1)),
            (_teleports.Teleports[0].Map, _teleports.Teleports[0].Location)
        );
    }

    [Fact]
    public async Task ANegativeHeight_IsAccepted()
    {
        await RunAsync("5690,569,-20");

        Assert.Equal(new Point3D(5690, 569, -20), Assert.Single(_teleports.Teleports).Location);
    }

    [Fact]
    public async Task TheTeleport_RunsOnTheGameLoop()
    {
        await RunAsync("1,1,1");
        var loopThread = 0;
        await _fixture!.ExecuteOnLoopAsync(() => loopThread = Environment.CurrentManagedThreadId);

        Assert.Equal(loopThread, _teleports.TeleportedOnThread);
    }

    [Theory,
     InlineData(),
     InlineData("1496,1628"),
     InlineData("1496,1628,ten"),
     InlineData("1496,1628,10,7"),
     InlineData("1496,1628,10", "Atlantis"),
     InlineData("1496,1628,10", "Felucca", "now"),
     InlineData("-1,1628,10"),
     InlineData("1496,1628,128"),
     InlineData("1496,1628,10", "7")]
    public async Task BadArguments_ShowTheUsage(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal((CommandOutputLevel.Error, Usage), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public async Task APlaceTheWorldDoesNotHave_IsRefusedWithAMessage()
    {
        _teleports.Result = false;

        var context = await RunAsync("1496,1628,10", "Tokuno");

        Assert.Equal(
            (CommandOutputLevel.Error, "You cannot go there: tokuno is not loaded or the spot is outside it."),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        _teleports.Result = false;

        var context = await RunAsync(
            ["1,1,1"],
            TestLocalization.With((30109, "Non puoi andare lì: {0} non è caricata o il punto è fuori dalla mappa."))
        );

        Assert.Equal(
            "Non puoi andare lì: trammel non è caricata o il punto è fuori dalla mappa.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task WithoutArguments_OpensTheGumpOfThePlacesOfYourMap()
    {
        _places.AddRange([Place(MapType.Felucca, "Towns", "Cove"), Place(MapType.Trammel, "Towns", "Haven")]);

        var context = await RunAsync();

        Assert.Empty(context.Output);
        Assert.Equal("go:Trammel", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public async Task WithoutArguments_OnAMapWithoutPlaces_OpensTheGumpOfTheMaps()
    {
        _places.Add(Place(MapType.Felucca, "Towns", "Cove"));

        await RunAsync();

        Assert.Equal("go:", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
    }

    [Theory, InlineData("cove"), InlineData("towns", "COVE"), InlineData("Towns  cove")]
    public async Task WithAName_TakesYouToThePlace_OfYourMapFirst(params string[] arguments)
    {
        _places.AddRange([Place(MapType.Felucca, "Towns", "Cove", 10), Place(MapType.Trammel, "Towns", "Cove", 20)]);

        var context = await RunAsync(arguments);

        var (mobile, map, location) = Assert.Single(_teleports.Teleports);
        Assert.Equal((new Serial(2), MapType.Trammel, new Point3D(20, 100, 5)), (mobile.Id, map, location));
        Assert.Empty(context.Output);
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public async Task WithANameOfAnotherMap_TakesYouToThatMap()
    {
        _places.Add(Place(MapType.Felucca, "Towns", "Cove", 10));

        await RunAsync("cove");

        Assert.Equal(
            (MapType.Felucca, new Point3D(10, 100, 5)),
            (_teleports.Teleports[0].Map, _teleports.Teleports[0].Location)
        );
    }

    [Fact]
    public async Task ANameSeveralPlacesHave_ListsThem_AndTakesYouNowhere()
    {
        _places.AddRange(
            [Place(MapType.Trammel, "Dungeons/Covetous", "Entrance"), Place(MapType.Trammel, "Dungeons/Shame", "Entrance")]
        );

        var context = await RunAsync("entrance");

        Assert.Empty(_teleports.Teleports);
        Assert.Equal(
            [
                "2 places are named entrance; add words of the category, such as go covetous entrance:",
                "Trammel: Dungeons/Covetous/Entrance",
                "Trammel: Dungeons/Shame/Entrance"
            ],
            context.Output.Select(line => line.Text)
        );
    }

    [Fact]
    public async Task ANameManyPlacesHave_ListsTheFirstTen()
    {
        _places.AddRange(
            Enumerable.Range(1, 13).Select(index => Place(MapType.Trammel, $"Dungeons/Cave {index}", "Entrance"))
        );

        var context = await RunAsync("entrance");

        Assert.Equal(12, context.Output.Count);
        Assert.Equal("Trammel: Dungeons/Cave 10/Entrance", context.Output[10].Text);
        Assert.Equal("... and 3 more.", context.Output[11].Text);
    }

    [Fact]
    public async Task ANameNoPlaceHas_SaysSo()
    {
        _places.Add(Place(MapType.Trammel, "Towns", "Cove"));

        var context = await RunAsync("atlantis", "bank");

        Assert.Equal(
            (CommandOutputLevel.Error, "No place is named atlantis bank; go alone lists them."),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public async Task ANamedPlaceTheWorldRefuses_IsRefusedWithAMessage()
    {
        _places.Add(Place(MapType.Trammel, "Towns", "Cove"));
        _teleports.Result = false;

        var context = await RunAsync("cove");

        Assert.Equal(
            "You cannot go there: trammel is not loaded or the spot is outside it.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task TheTextsOfTheNames_AreInTheServerLanguage()
    {
        _places.Add(Place(MapType.Trammel, "Towns", "Cove"));

        var context = await RunAsync(
            ["atlantis"],
            TestLocalization.With((30115, "Nessun luogo si chiama {0}; go da solo li elenca."))
        );

        Assert.Equal("Nessun luogo si chiama atlantis; go da solo li elenca.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("go", "go", ["1,1,1"], CommandSourceType.Console, null);

        await new GoCommand(_teleports, _mobiles, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Empty(_teleports.Teleports);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization)
    {
        _fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(_fixture.Loop);
        var session = sessions.GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(
            new MobileEntity
                { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) }
        );
        var context = new CommandContext(".go", "go", arguments, CommandSourceType.InGame, session);

        // The gump shows the path it was opened with.
        var templates = new GumpTemplateService(
            _gumps,
            new StubDataLoaderService().With(
                new GumpTemplate
                {
                    Id = "go", File = "go.xml",
                    Root = XElement.Parse("""<gump id="go"><text x="1" y="1">go:${path}</text></gump>""")
                }
            ),
            _fixture.Loop,
            sessions
        );
        var gumps = new GumpModule(
            sessions,
            _gumps,
            templates,
            new Lazy<IGumpScriptService>(new RecordingGumpScriptService()),
            _fixture.Loop
        );
        var locations = new LocationService(new StubDataLoaderService().With(_places.ToArray()), TestSectors.Create());

        await new GoCommand(_teleports, _mobiles, _fixture.Loop, localization, locations, gumps).ExecuteAsync(context);

        return context;
    }

    private static NamedLocation Place(MapType map, string category, string name, int x = 100)
    {
        return new() { Map = map, Category = category, Name = name, Location = new Point3D(x, 100, 5) };
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
