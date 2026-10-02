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
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class SeasonCommandTests : IAsyncDisposable
{
    private readonly StubSeasonService _seasons = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private SessionFixture? _fixture;

    [Fact]
    public async Task WithoutASeason_PrintsTheSeasonHereAndTheMaps()
    {
        var context = await RunAsync();

        Assert.Equal("Season here: fall; trammel: summer.", Assert.Single(context.Output).Text);
        Assert.Empty(_seasons.Overrides);
    }

    [Theory, InlineData("winter"), InlineData("Winter")]
    public async Task WithASeason_SetsItOnYourMap_OnTheGameLoop(string season)
    {
        var context = await RunAsync(season);
        var loopThread = 0;
        await _fixture!.ExecuteOnLoopAsync(() => loopThread = Environment.CurrentManagedThreadId);

        Assert.Equal((MapType.Trammel, SeasonType.Winter), Assert.Single(_seasons.Overrides));
        Assert.Equal(loopThread, _seasons.SetOnThread);
        Assert.Equal("Season of trammel: summer.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task Auto_ClearsTheMapsSeason()
    {
        await RunAsync("auto");

        Assert.Equal((MapType.Trammel, null), Assert.Single(_seasons.Overrides));
    }

    [Theory, InlineData("monsoon"), InlineData("winter now")]
    public async Task ABadSeason_ShowsTheUsage(string arguments)
    {
        var context = await RunAsync(arguments.Split(' '));

        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: season [spring|summer|fall|winter|desolation|auto]"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
        Assert.Empty(_seasons.Overrides);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("season", "season", [], CommandSourceType.Console, null);

        await new SeasonCommand(_seasons, _mobiles, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync([], TestLocalization.With((30097, "Stagione qui: {0}; {1}: {2}.")));

        Assert.Equal("Stagione qui: fall; trammel: summer.", Assert.Single(context.Output).Text);
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
        var context = new CommandContext(".season", "season", arguments, CommandSourceType.InGame, session);

        await new SeasonCommand(_seasons, _mobiles, _fixture.Loop, localization).ExecuteAsync(context);

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
