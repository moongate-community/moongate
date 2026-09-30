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
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class WeatherCommandTests : IAsyncDisposable
{
    private readonly StubWeatherService _weather = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private SessionFixture? _fixture;

    [Fact]
    public async Task WithoutAKind_PrintsTheWeatherWhereYouStand()
    {
        var context = await RunAsync();

        Assert.Equal("Weather here (temperate): rain, density 40, temperature 12.", Assert.Single(context.Output).Text);
        Assert.Empty(_weather.Forced);
    }

    [Fact]
    public async Task WithAKind_ForcesItOnYourProfile()
    {
        var context = await RunAsync("storm");

        Assert.Equal([("temperate", WeatherKindType.Storm)], _weather.Forced);
        Assert.Equal("The weather of temperate is now storm until the next hour.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData("hail"), InlineData("rain now")]
    public async Task ABadKind_ShowsTheUsage(string arguments)
    {
        var context = await RunAsync(arguments.Split(' '));

        Assert.Equal((CommandOutputLevel.Error, "Usage: weather [none|rain|snow|storm]"), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Empty(_weather.Forced);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("weather", "weather", [], CommandSourceType.Console, null);

        await new WeatherCommand(_weather, _mobiles).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(["snow"], TestLocalization.With((30064, "Il meteo di {0} ora è {1} fino alla prossima ora.")));

        Assert.Equal("Il meteo di temperate ora è snow fino alla prossima ora.", Assert.Single(context.Output).Text);
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
        var context = new CommandContext(".weather", "weather", arguments, CommandSourceType.InGame, session);

        await new WeatherCommand(_weather, _mobiles, localization).ExecuteAsync(context);

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
