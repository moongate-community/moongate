using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class WhereCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();

    private SessionFixture? _fixture;

    [Fact]
    public async Task ExecuteAsync_AnObject_PrintsItsSerial()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x40000012));

        var context = await RunAsync();

        Assert.Equal("0x40000012", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_ALocation_PrintsMapAndPoint()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1496, 1628, 10));

        var context = await RunAsync();

        Assert.Equal("Trammel (1496, 1628, 10)", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_Canceled_SaysSo()
    {
        var context = await RunAsync();

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_FromTheConsole_IsRefused()
    {
        var context = new CommandContext("where", "where", [], CommandSourceType.Console, null);

        await new WhereCommand(_targets).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(TestLocalization.With((30008, "Bersaglio annullato.")));

        Assert.Equal("Bersaglio annullato.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".where", "where", [], CommandSourceType.InGame, session);

        await new WhereCommand(_targets, localization).ExecuteAsync(context);

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
