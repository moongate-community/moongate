using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class RemoveCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly StubNpcService _npcs = new();

    private SessionFixture? _fixture;

    [Fact]
    public async Task ExecuteAsync_AnNpc_RemovesIt()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x00000100));

        var context = await RunAsync();

        Assert.Equal([new Serial(0x00000100)], _npcs.Removals);
        Assert.Equal("Removed 0x00000100.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_NotAnNpc_SaysSo()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x00000002));
        _npcs.Removes = false;

        var context = await RunAsync();

        Assert.Equal("Not an NPC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_ALocation_SaysCanceled()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0));

        var context = await RunAsync();

        Assert.Equal("Canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_FromTheConsole_IsRefused()
    {
        var context = new CommandContext("remove", "remove", [], CommandSourceType.Console, null);

        await new RemoveCommand(_npcs, _targets).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    private async Task<CommandContext> RunAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".remove", "remove", [], CommandSourceType.InGame, session);

        await new RemoveCommand(_npcs, _targets).ExecuteAsync(context);

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
