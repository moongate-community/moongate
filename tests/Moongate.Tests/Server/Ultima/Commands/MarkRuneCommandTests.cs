using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class MarkRuneCommandTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly StubTargetService _targets = new();
    private readonly StubItemHandlingService _handling = new();

    private readonly RegionService _regions = new(
        new StubDataLoaderService().With(
            new RegionContent
            {
                Map = MapType.Felucca, Name = "Britain", RuneName = "Britain",
                Areas = [new RegionAreaContent { X1 = 100, Y1 = 100, X2 = 200, Y2 = 200 }]
            }
        )
    );

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _gm = null!;
    private ItemEntity _rune = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _gm!));
        (_gm.Map, _gm.Location) = (MapType.Felucca, new Point3D(150, 160, 7));
        _rune = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "recall_rune", ItemId = 0x1F14, Amount = 1 };
        _items.Add([_rune]);
    }

    [Fact]
    public async Task ARune_IsMarkedWithTheGameMastersPlace_AndNamedForTheRegion()
    {
        _targets.Result = TargetResult.ForObject(_rune.Id);

        var context = await RunAsync();

        Assert.Equal(
            (true, 150L, 160L, 7L, (long)MapType.Felucca),
            (
                _rune.GetProp<bool>("rune.marked"),
                _rune.GetProp<long>("rune.x"),
                _rune.GetProp<long>("rune.y"),
                _rune.GetProp<long>("rune.z"),
                _rune.GetProp<long>("rune.map")
            )
        );
        Assert.Equal("a recall rune for Britain", _rune.Name);
        Assert.Equal("The rune is marked for Britain.", Assert.Single(context.Output).Text);
        Assert.Equal(["recall_rune"], _handling.Refreshed);
    }

    [Fact]
    public async Task APlaceOutsideEveryRegion_NamesTheRuneForTheMap()
    {
        _gm.Location = new Point3D(500, 500, 0);
        _targets.Result = TargetResult.ForObject(_rune.Id);

        await RunAsync();

        Assert.Equal("a recall rune for Felucca", _rune.Name);
    }

    [Fact]
    public async Task AnItemThatIsNotARune_IsRefused_AndChangesNothing()
    {
        var stone = new ItemEntity { Id = new Serial(0x40000011), TemplateId = "stone", ItemId = 0x1363, Amount = 1 };
        _items.Add([stone]);
        _targets.Result = TargetResult.ForObject(stone.Id);

        var context = await RunAsync();

        Assert.Equal("That is not a recall rune.", Assert.Single(context.Output).Text);
        Assert.Null(stone.Props);
        Assert.Empty(_handling.Refreshed);
    }

    [Fact]
    public async Task ACanceledTarget_ChangesNothing()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Felucca, new Point3D(1, 1, 0));

        var context = await RunAsync();

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.Null(_rune.Props);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("mark_rune", "mark_rune", [], CommandSourceType.Console, null);

        await Command(null).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        _targets.Result = TargetResult.ForObject(_rune.Id);

        var context = await RunAsync(TestLocalization.With((30242, "La runa è segnata per {0}.")));

        Assert.Equal("La runa è segnata per Britain.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(ILocalizationService? localization = null)
    {
        var context = new CommandContext(".mark_rune", "mark_rune", [], CommandSourceType.InGame, _session);

        await Command(localization).ExecuteAsync(context);

        return context;
    }

    private MarkRuneCommand Command(ILocalizationService? localization)
    {
        return new(_targets, _items, _fixture.Mobiles, _regions, _handling, _fixture.Network.Loop, localization);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
