using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class SetCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Hits = 50, HitsMax = 50, Mana = 20, ManaMax = 20, Stamina = 30,
        StaminaMax = 30
    };

    private SessionFixture? _fixture;

    public SetCommandTests()
    {
        _mobiles.EnterWorld(_orc);
        _targets.Result = TargetResult.ForObject(_orc.Id);
    }

    [Theory,
     InlineData("hits", 10, "an orc: hits is now 10."),
     InlineData("MANA", 5, "an orc: mana is now 5."),
     InlineData("stamina", 0, "an orc: stamina is now 0."),
     // Hunger stays from 0 to 20.
     InlineData("hunger", 99, "an orc: hunger is now 20.")]
    public async Task ExecuteAsync_SetsTheNumberOfTheTargetedMobile_AndSaysWhatItIsNow(string what, int value, string said)
    {
        var context = await RunAsync(what, value.ToString());

        Assert.Equal(said, Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_Hits_GoThroughTheStateService_SoTheClientsAreTold()
    {
        await RunAsync("hits", "10");

        Assert.Equal((_orc, 10), (Assert.Single(_state.Stats).Mobile, _state.Stats[0].Change.Hits));
        Assert.Equal(10, _orc.Hits);
    }

    [Theory, InlineData(), InlineData("hits"), InlineData("luck", "5"), InlineData("hits", "ten"), InlineData("hits", "-1"), InlineData("hits", "1", "2")]
    public async Task ExecuteAsync_WithoutAKnownNumberAndAValue_PrintsTheUsage(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: set <hits|mana|stamina|hunger> <value>"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_AnItemOrASpot_ChangesNothing()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x40000001));
        var item = await RunAsync("hits", "10");
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0));
        var spot = await RunAsync("hits", "10");

        Assert.Equal("That is not a character or an NPC.", Assert.Single(item.Output).Text);
        Assert.Equal("Target canceled.", Assert.Single(spot.Output).Text);
        Assert.Empty(_state.Stats);
    }

    [Fact]
    public async Task ExecuteAsync_FromTheConsole_IsRefused()
    {
        var context = new CommandContext("set hits 10", "set", ["hits", "10"], CommandSourceType.Console, null);

        await NewCommand().ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    private SetCommand NewCommand()
    {
        var hunger = new HungerService(
            new RecordingTimerService(),
            new SessionService(new StubGameLoop()),
            _mobiles,
            new RecordingSpeechService(),
            new RegenerationConfig(),
            new SettableClock()
        );

        return new(_targets, _mobiles, _state, hunger, new StubGameLoop());
    }

    private async Task<CommandContext> RunAsync(params string[] arguments)
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }

        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".set", "set", arguments, CommandSourceType.InGame, session);

        await NewCommand().ExecuteAsync(context);

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
