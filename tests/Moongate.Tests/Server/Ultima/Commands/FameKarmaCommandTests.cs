using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class FameKarmaCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly MobileEntity _bran = new()
    {
        Id = new Serial(3), Name = "Bran", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0)
    };

    private SessionFixture? _fixture;

    public FameKarmaCommandTests()
    {
        _mobiles.EnterWorld(_bran);
    }

    [Fact]
    public async Task Fame_OnATargetedMobile_SetsIt()
    {
        _targets.Result = TargetResult.ForObject(_bran.Id);

        var context = await RunAsync("fame", "10000");

        Assert.Equal(10000, _bran.Fame);
        Assert.Equal("Bran now has 10000 fame.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task Karma_OnATargetedMobile_SetsItNegativeToo()
    {
        _targets.Result = TargetResult.ForObject(_bran.Id);

        var context = await RunAsync("karma", "-2500");

        Assert.Equal(-2500, _bran.Karma);
        Assert.Equal("Bran now has -2500 karma.", Assert.Single(context.Output).Text);
    }

    [Theory,
     InlineData("fame", "-1"),
     InlineData("fame", "32001"),
     InlineData("fame", "lots"),
     InlineData("fame", null),
     InlineData("karma", "-32001"),
     InlineData("karma", "32001")]
    public async Task AValueOutOfRangeOrMissing_ShowsTheUsageWithoutATarget(string command, string? value)
    {
        var context = await RunAsync(command, value);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.StartsWith("Usage: ", context.Output[0].Text);
        Assert.Equal(0, _targets.Requests);
        Assert.Equal((0, 0), (_bran.Fame, _bran.Karma));
    }

    [Fact]
    public async Task AnItemOrUnknownSerial_IsNotAMobile()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x40000001));

        var context = await RunAsync("fame", "100");

        Assert.Equal("That is not a character or an NPC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ACanceledTarget_ChangesNothing()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0));

        var context = await RunAsync("karma", "100");

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.Equal(0, _bran.Karma);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("fame 1", "fame", ["1"], CommandSourceType.Console, null);

        await new FameCommand(_targets, _mobiles, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        _targets.Result = TargetResult.ForObject(_bran.Id);

        var context = await RunAsync("fame", "5", TestLocalization.With((30050, "{0} ora ha {1} di fama.")));

        Assert.Equal("Bran ora ha 5 di fama.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(string command, string? value, ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        string[] arguments = value is null ? [] : [value];
        var context = new CommandContext($".{command}", command, arguments, CommandSourceType.InGame, session);
        ICommandExecutor executor = command == "fame"
            ? new FameCommand(_targets, _mobiles, _fixture.Loop, localization)
            : new KarmaCommand(_targets, _mobiles, _fixture.Loop, localization);

        await executor.ExecuteAsync(context);

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
