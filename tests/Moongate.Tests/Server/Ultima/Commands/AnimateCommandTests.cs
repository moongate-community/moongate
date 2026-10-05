using Moongate.Core.Geometry;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class AnimateCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _orc = new()
    {
        Id = new(0x100), Name = "an orc", Map = MapType.Felucca, Location = new Point3D(102, 100, 0)
    };

    private SessionFixture? _fixture;

    public AnimateCommandTests()
    {
        _mobiles.EnterWorld(_orc);
    }

    [Fact]
    public async Task ExecuteAsync_MakesTheMobileTargetedPlayTheAction_OnceWithEnoughFrames()
    {
        _targets.Result = TargetResult.ForObject(_orc.Id);

        var context = await RunAsync("21");

        Assert.Equal(["Animated 256 21 10 1"], _view.Calls);
        Assert.Equal("an orc plays action 21.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData(), InlineData("fall"), InlineData("-1"), InlineData("65536"), InlineData("1", "2")]
    public async Task ExecuteAsync_WithoutAnActionInRange_SaysTheUsage_AndAsksForNoTarget(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Empty(_view.Calls);
        Assert.Equal(0, _targets.Requests);
        Assert.Contains("animate <0..65535>", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_OnWhatIsNoMobile_SaysSo()
    {
        _targets.Result = TargetResult.ForObject(new(0x40000010));

        var context = await RunAsync("21");

        Assert.Empty(_view.Calls);
        Assert.Equal("That is not a character or an NPC.", Assert.Single(context.Output).Text);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private async Task<CommandContext> RunAsync(params string[] arguments)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".animate", "animate", arguments, CommandSourceType.InGame, session);

        await new AnimateCommand(_targets, _mobiles, _view, new StubGameLoop()).ExecuteAsync(context);

        return context;
    }
}
