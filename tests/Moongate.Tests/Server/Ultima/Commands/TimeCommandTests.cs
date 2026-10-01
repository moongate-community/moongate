using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class TimeCommandTests : IAsyncDisposable
{
    private readonly SettableClock _now = new() { Now = new DateTimeOffset(1997, 9, 1, 0, 0, 0, TimeSpan.Zero) };
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private SessionFixture? _fixture;

    [Fact]
    public async Task InGame_PrintsTheGameTimeWhereYouStand()
    {
        // 5 game minutes per 25 seconds, plus 320 minutes for Trammel and 100 for x 1600.
        _now.Advance(TimeSpan.FromSeconds(25));

        var context = await RunAsync();

        // The moons: Felucca (5 + 100) / 10 = 10, Trammel (5 + 320 + 100) / 30 = 14, modulo 8.
        Assert.Equal(
            ["Game time here: 07:05.", "Moons: Trammel last quarter, Felucca first quarter."],
            context.Output.Select(line => line.Text)
        );
    }

    [Fact]
    public async Task WithArguments_ShowsTheUsage()
    {
        var context = await RunAsync(["now"]);

        Assert.Equal((CommandOutputLevel.Error, "Usage: time"), (Assert.Single(context.Output).Level, context.Output[0].Text));
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("time", "time", [], CommandSourceType.Console, null);

        await new TimeCommand(new ClockService(_now, new WorldConfig()), _mobiles).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync([], TestLocalization.With((30094, "Ora di gioco qui: {0}.")));

        Assert.Equal("Ora di gioco qui: 07:00.", context.Output[0].Text);
    }

    private async Task<CommandContext> RunAsync(string[]? arguments = null, ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        var context = new CommandContext(".time", "time", arguments ?? [], CommandSourceType.InGame, session);

        await new TimeCommand(new ClockService(_now, new WorldConfig()), _mobiles, localization).ExecuteAsync(context);

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
