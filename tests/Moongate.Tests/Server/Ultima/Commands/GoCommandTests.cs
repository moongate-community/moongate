using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class GoCommandTests : IAsyncDisposable
{
    private const string Usage = "Usage: go <x>,<y>,<z> [map]";

    private readonly RecordingTeleportService _teleports = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private SessionFixture? _fixture;

    [Theory,
     InlineData("1496,1628,10"),
     InlineData("1496", "1628", "10"),
     InlineData("1496,", "1628,", "10"),
     InlineData("1496,1628", "10")]
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

        Assert.Equal((MapType.Felucca, new Point3D(1, 1, 1)), (_teleports.Teleports[0].Map, _teleports.Teleports[0].Location));
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

        Assert.Equal("Non puoi andare lì: trammel non è caricata o il punto è fuori dalla mappa.", Assert.Single(context.Output).Text);
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
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        var context = new CommandContext(".go", "go", arguments, CommandSourceType.InGame, session);

        await new GoCommand(_teleports, _mobiles, _fixture.Loop, localization).ExecuteAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
