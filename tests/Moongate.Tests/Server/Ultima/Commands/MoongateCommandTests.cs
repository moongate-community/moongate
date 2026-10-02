using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class MoongateCommandTests : IAsyncDisposable
{
    private const string Usage = "Usage: moongate <x>,<y>,<z> [map]";

    private static readonly Point3D Feet = new(1600, 1600, 5);

    private readonly SectorService _sectors = TestSectors.Create();
    private readonly MobileService _mobiles;
    private readonly ItemService _items;
    private readonly RecordingWorldViewService _view = new();
    private readonly FakeItemFactoryService _factory = new(
        new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "moongate", ItemId = new Serial(0x0F6C), Name = "moongate", ScriptId = "moongate", Movable = false }
            )
        ),
        new FakeTileDataService()
    );

    private SessionFixture? _fixture;

    public MoongateCommandTests()
    {
        _mobiles = new(new StubMovementService(), _sectors);
        _items = TestItems.Create(_sectors);
    }

    [Theory, InlineData("2500,500,-3"), InlineData("2500", "500", "-3")]
    public async Task WithThreeNumbers_PutsAGateToThatPlaceOfYourMap_AtYourFeet(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        var gate = Assert.Single(_sectors.GetItemsInRange(MapType.Trammel, Feet, 0));
        Assert.Equal(("moongate", 0x0F6C, (Point3D?)Feet), (gate.TemplateId, gate.ItemId, gate.GroundLocation));
        Assert.True(gate.Id.IsItem);
        Assert.Equal(
            [2500L, 500L, -3L, (long)MapType.Trammel],
            new[] { "teleport.x", "teleport.y", "teleport.z", "teleport.map" }.Select(key => gate.Props![key])
        );
        Assert.Contains(gate, Assert.Single(_factory.Saved));
        // The first serial the fake factory gives: 0x40000001.
        Assert.Equal("Appeared 1073741825", Assert.Single(_view.Calls));
        Assert.Equal("A moongate to trammel (2500, 500, -3) is at your feet.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData("Felucca"), InlineData("felucca")]
    public async Task WithAMap_TheGateLeadsToThatMap(string map)
    {
        await RunAsync("2500,500,0", map);

        Assert.Equal((long)MapType.Felucca, Assert.Single(_sectors.GetItemsInRange(MapType.Trammel, Feet, 0)).Props!["teleport.map"]);
    }

    [Theory,
     InlineData(),
     InlineData("2500,500"),
     InlineData("2500,500,ten"),
     InlineData("2500,500,0", "Atlantis"),
     InlineData("2500,500,0", "7"),
     InlineData("2500,500,128")]
    public async Task BadArguments_ShowTheUsage_AndPutNoGate(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal((CommandOutputLevel.Error, Usage), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Empty(_factory.Saved);
    }

    [Theory, InlineData("2500,500,0", "Tokuno"), InlineData("9000,500,0")]
    public async Task APlaceTheWorldDoesNotHave_IsRefused_AndPutsNoGate(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.EndsWith("is not loaded or the spot is outside it.", context.Output[0].Text);
        Assert.Empty(_factory.Saved);
        Assert.Empty(_sectors.GetItemsInRange(MapType.Trammel, Feet, 0));
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(["1,1,1"], TestLocalization.With((30111, "Un moongate per {0} ({1}, {2}, {3}) è ai tuoi piedi.")));

        Assert.Equal("Un moongate per trammel (1, 1, 1) è ai tuoi piedi.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("moongate", "moongate", ["1,1,1"], CommandSourceType.Console, null);

        await new MoongateCommand(_mobiles, _sectors, _items, _factory, _view, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Empty(_factory.Saved);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = Feet });
        var context = new CommandContext(".moongate", "moongate", arguments, CommandSourceType.InGame, session);

        await new MoongateCommand(_mobiles, _sectors, _items, _factory, _view, _fixture.Loop, localization).ExecuteAsync(context);

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
