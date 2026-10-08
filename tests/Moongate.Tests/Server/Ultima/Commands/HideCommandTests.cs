using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class HideCommandTests : IAsyncDisposable
{
    private static readonly Serial Gm = new(0x00000009);

    private readonly StubGameLoop _loop = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly RecordingEffectService _effects = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly MobileEntity _gm = new()
    {
        Id = Gm, Name = "Gabriel", AccountId = new Serial(0x42), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0)
    };

    private SessionFixture? _fixture;

    public HideCommandTests()
    {
        _mobiles.EnterWorld(_gm);
    }

    [Fact]
    public async Task Hide_HidesTheGameMaster_InAPuffOfSmoke_AndSaysSo()
    {
        var context = await RunAsync("hide", hidden: true);

        Assert.True(_gm.Hidden);
        Assert.Equal(("You are hidden: players cannot see you."), Assert.Single(context.Output).Text);
        Assert.Equal((int)EffectGraphicType.Smoke, Assert.Single(_effects.At).Options.Graphic);
        Assert.Equal((MapType.Trammel, _gm.Location, 0x228), Assert.Single(_speech.PlacedSounds));
    }

    [Fact]
    public async Task Unhide_ShowsTheGameMasterAgain()
    {
        _gm.Hidden = true;

        var context = await RunAsync("unhide", hidden: false);

        Assert.False(_gm.Hidden);
        Assert.Equal("You are visible again.", Assert.Single(context.Output).Text);
        Assert.Single(_effects.At);
    }

    [Theory]
    [InlineData("hide", true)]
    [InlineData("unhide", false)]
    public async Task HideOrUnhide_WhenItIsSoAlready_SaysSo_WithNoSmoke(string name, bool hidden)
    {
        _gm.Hidden = hidden;

        var context = await RunAsync(name, hidden);

        Assert.Equal("You are already that way.", Assert.Single(context.Output).Text);
        Assert.Empty(_effects.At);
        Assert.Empty(_speech.PlacedSounds);
    }

    [Fact]
    public async Task Hide_FromTheConsole_WorksInGameOnly()
    {
        var context = new CommandContext(".hide", "hide", [], CommandSourceType.Console, null);

        await new HideCommand(_mobiles, _state, _loop).ExecuteAsync(context);

        Assert.False(_gm.Hidden);
        Assert.Contains("works in game only", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(string name, bool hidden)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, Gm));
        var context = new CommandContext($".{name}", name, [], CommandSourceType.InGame, session);

        if (hidden)
        {
            await new HideCommand(_mobiles, _state, _loop, _effects, _speech).ExecuteAsync(context);
        }
        else
        {
            await new UnhideCommand(_mobiles, _state, _loop, _effects, _speech).ExecuteAsync(context);
        }

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
