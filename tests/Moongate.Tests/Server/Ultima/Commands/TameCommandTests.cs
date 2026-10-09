using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class TameCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "horse", Tags = new() { [MountProps.MountItemTag] = "horse4" } },
            new MobileTemplate { Id = "orc" }
        )
    );

    private readonly MobileEntity _staff = new()
    {
        Id = new(2), Name = "Giachi", AccountId = new Serial(1002), Map = MapType.Felucca,
        Location = new Point3D(100, 100, 0)
    };

    private readonly MobileEntity _aria = new()
    {
        Id = new(3), Name = "Aria", AccountId = new Serial(1003), Map = MapType.Felucca, Location = new Point3D(101, 100, 0)
    };

    private readonly MobileEntity _horse = new()
    {
        Id = new(0x100), Name = "a horse", TemplateId = "horse", Map = MapType.Felucca, Location = new Point3D(102, 100, 0)
    };

    private readonly MobileEntity _orc = new()
    {
        Id = new(0x101), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca, Location = new Point3D(103, 100, 0)
    };

    private SessionFixture? _fixture;

    public TameCommandTests()
    {
        _mobiles.EnterWorld(_staff);
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_horse);
        _mobiles.EnterWorld(_orc);
    }

    [Fact]
    public async Task ExecuteAsync_AHorse_BecomesTheGameMastersOwn()
    {
        _targets.Result = TargetResult.ForObject(_horse.Id);

        var context = await RunAsync();

        Assert.Equal((long)_staff.Id.Value, _horse.GetProp<long>(MountProps.Owner));
        Assert.Equal("a horse now belongs to Giachi.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_WithAPlayerName_GivesTheHorseToThatPlayer()
    {
        _targets.Result = TargetResult.ForObject(_horse.Id);

        var context = await RunAsync("aria");

        Assert.Equal((long)_aria.Id.Value, _horse.GetProp<long>(MountProps.Owner));
        Assert.Equal("a horse now belongs to Aria.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownName_ChangesNothing()
    {
        _targets.Result = TargetResult.ForObject(_horse.Id);

        var context = await RunAsync("Nobody");

        Assert.False(_horse.TryGetProp<long>(MountProps.Owner, out _));
        Assert.Equal("No character is named Nobody.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_ACreatureThatIsNoMount_IsRefused()
    {
        _targets.Result = TargetResult.ForObject(_orc.Id);

        var context = await RunAsync();

        Assert.False(_orc.TryGetProp<long>(MountProps.Owner, out _));
        Assert.Equal("an orc cannot be tamed.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_APlayer_IsRefused()
    {
        _targets.Result = TargetResult.ForObject(_aria.Id);

        var context = await RunAsync();

        Assert.False(_aria.TryGetProp<long>(MountProps.Owner, out _));
        Assert.Equal("Aria cannot be tamed.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData(0x40000010u), InlineData(0x00000999u)]
    public async Task ExecuteAsync_AnItemOrSomeoneGone_SaysItIsNotAnNpc(uint serial)
    {
        _targets.Result = TargetResult.ForObject(new Serial(serial));

        var context = await RunAsync();

        Assert.Equal("That is not an NPC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_TheCursorPutAway_TamesNothing()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        var context = await RunAsync();

        Assert.False(_horse.TryGetProp<long>(MountProps.Owner, out _));
        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(params string[] arguments)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, _staff.Id));
        var context = new CommandContext(".tame", "tame", arguments, CommandSourceType.InGame, session);

        await new TameCommand(_targets, _mobiles, _templates, new StubGameLoop()).ExecuteAsync(context);

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
