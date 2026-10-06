using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class KillCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly StubDeathService _death = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _staff = new()
    {
        Id = new(2), Name = "Giachi", AccountId = new Serial(1002), Map = MapType.Felucca, Location = new Point3D(100, 100, 0)
    };
    private readonly MobileEntity _aria = new()
    {
        Id = new(3), Name = "Aria", AccountId = new Serial(1003), Map = MapType.Felucca, Location = new Point3D(101, 100, 0)
    };
    private readonly MobileEntity _orc = new()
    {
        Id = new(0x100), Name = "an orc", Map = MapType.Felucca, Location = new Point3D(102, 100, 0)
    };

    private SessionFixture? _fixture;

    public KillCommandTests()
    {
        _mobiles.EnterWorld(_staff);
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_orc);
    }

    [Fact]
    public async Task ExecuteAsync_AnNpc_KillsIt_WithTheGameMasterAsItsKiller()
    {
        _targets.Result = TargetResult.ForObject(_orc.Id);

        var context = await RunAsync();

        Assert.Equal((_orc, _staff), Assert.Single(_death.Killed));
        Assert.Equal("an orc is dead.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_APlayer_IsKilledLikeAnNpc()
    {
        _targets.Result = TargetResult.ForObject(_aria.Id);

        var context = await RunAsync();

        Assert.Equal((_aria, _staff), Assert.Single(_death.Killed));
        Assert.Equal($"{_aria.Name} is dead.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_APlayerThatCannotDie_SaysSo()
    {
        _death.Kills = false;
        _targets.Result = TargetResult.ForObject(_aria.Id);

        var context = await RunAsync();

        Assert.Equal($"{_aria.Name} cannot die.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData(0x40000010u), InlineData(0x00000999u)]
    public async Task ExecuteAsync_AnItemOrSomeoneGone_SaysItIsNotAnNpc(uint serial)
    {
        _targets.Result = TargetResult.ForObject(new Serial(serial));

        var context = await RunAsync();

        Assert.Empty(_death.Killed);
        Assert.Equal("That is not an NPC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_AnNpcTheDeathRefuses_SaysItIsNotAnNpc()
    {
        _targets.Result = TargetResult.ForObject(_orc.Id);
        _death.Kills = false;

        var context = await RunAsync();

        Assert.Equal("That is not an NPC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_TheCursorPutAway_KillsNobody()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        var context = await RunAsync();

        Assert.Empty(_death.Killed);
        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private async Task<CommandContext> RunAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, _staff.Id));
        var context = new CommandContext(".kill", "kill", [], CommandSourceType.InGame, session);

        await new KillCommand(_death, _targets, _mobiles, new StubGameLoop()).ExecuteAsync(context);

        return context;
    }
}
