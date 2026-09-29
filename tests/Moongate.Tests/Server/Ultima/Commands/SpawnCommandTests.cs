using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class SpawnCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly StubNpcService _npcs = new();
    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(new MobileTemplate { Id = "orc" })
    );

    private SessionFixture? _fixture;

    [Fact]
    public async Task ExecuteAsync_WithoutATemplate_PrintsTheUsage()
    {
        var context = await RunAsync();

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_AnUnknownTemplate_SaysSoWithoutACursor()
    {
        var context = await RunAsync("dragon");

        Assert.Equal("Unknown mobile template: dragon", Assert.Single(context.Output).Text);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_ALocation_SpawnsThereAndPrintsIt()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1496, 1628, 0));

        var context = await RunAsync("orc");

        Assert.Equal(("orc", MapType.Trammel, new Point3D(1496, 1628, 0)), Assert.Single(_npcs.Spawns));
        Assert.Equal("Spawned Orc (0x00000100) at Trammel (1496, 1628, 0).", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_Canceled_SpawnsNothing()
    {
        var context = await RunAsync("orc");

        Assert.Equal("Canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public async Task ExecuteAsync_AnObjectTarget_SpawnsNothing()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x40000001));

        var context = await RunAsync("orc");

        Assert.Equal("Canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public async Task ExecuteAsync_TheSpawnFails_ReportsTheError()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1496, 1628, 0));
        _npcs.SpawnFailure = new ArgumentOutOfRangeException("location", "outside the map");

        var context = await RunAsync("orc");

        var line = Assert.Single(context.Output);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Contains("outside the map", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FromTheConsole_IsRefused()
    {
        var context = new CommandContext("spawn orc", "spawn", ["orc"], CommandSourceType.Console, null);

        await new SpawnCommand(_npcs, _templates, _targets).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    private async Task<CommandContext> RunAsync(params string[] arguments)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".spawn", "spawn", arguments, CommandSourceType.InGame, session);

        await new SpawnCommand(_npcs, _templates, _targets).ExecuteAsync(context);

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
