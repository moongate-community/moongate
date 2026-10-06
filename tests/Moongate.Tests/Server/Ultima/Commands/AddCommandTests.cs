using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class AddCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly StubItemSpawnService _spawns = new();

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "treasure_chest_level_1", ItemId = new Serial(0x0E43), Name = "treasure chest" }
        )
    );

    private SessionFixture? _fixture;

    [Theory, InlineData(), InlineData("chest", "extra")]
    public async Task ExecuteAsync_WithoutOneTemplate_PrintsTheUsage(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: add <template>"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_AnUnknownTemplate_SaysSoWithoutACursor()
    {
        var context = await RunAsync("throne");

        Assert.Equal("Unknown item template: throne", Assert.Single(context.Output).Text);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_ALocation_PutsTheItemThereAndPrintsIt()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1385, 1490, 10));

        var context = await RunAsync("treasure_chest_level_1");

        Assert.Equal(
            ("treasure_chest_level_1", MapType.Trammel, new Point3D(1385, 1490, 10)),
            Assert.Single(_spawns.Spawns)
        );
        Assert.Equal(
            "Added treasure_chest_level_1 (0x40001000) at Trammel (1385, 1490, 10).",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task ExecuteAsync_CanceledOrAnObject_AddsNothing()
    {
        var canceled = await RunAsync("treasure_chest_level_1");
        _targets.Result = TargetResult.ForObject(new Serial(0x40000001));
        var onObject = await RunAsync("treasure_chest_level_1");

        Assert.Equal("Target canceled.", Assert.Single(canceled.Output).Text);
        Assert.Equal("Target canceled.", Assert.Single(onObject.Output).Text);
        Assert.Empty(_spawns.Spawns);
    }

    [Fact]
    public async Task ExecuteAsync_TheAddFails_ReportsTheError()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1385, 1490, 10));
        _spawns.SpawnFailure = new ArgumentOutOfRangeException("location", "outside the map");

        var context = await RunAsync("treasure_chest_level_1");

        // The exception goes to the log, not to the GM.
        Assert.Equal(
            (CommandOutputLevel.Error, "The item could not be added. Check the server logs."),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task ExecuteAsync_FromTheConsole_IsRefused()
    {
        var context = new CommandContext("add chest", "add", ["chest"], CommandSourceType.Console, null);

        await new AddCommand(_spawns, _templates, _targets).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(TestLocalization.With((30118, "Modello di oggetto sconosciuto: {0}")), "nothing");

        Assert.Equal("Modello di oggetto sconosciuto: nothing", Assert.Single(context.Output).Text);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(null, arguments);
    }

    private async Task<CommandContext> RunAsync(ILocalizationService? localization, params string[] arguments)
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }

        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".add", "add", arguments, CommandSourceType.InGame, session);

        await new AddCommand(_spawns, _templates, _targets, localization).ExecuteAsync(context);

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
