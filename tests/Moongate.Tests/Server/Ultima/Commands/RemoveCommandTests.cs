using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class RemoveCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly StubNpcService _npcs = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly ItemService _items = TestItems.Create();

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

        Assert.Equal("That is not an NPC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_AnItemOnTheGround_RemovesItWithWhatItHolds()
    {
        var chest = Item(0x40000010);
        var gold = Item(0x40000011);
        var deep = Item(0x40000012);
        chest.PlaceOnGround(MapType.Trammel, new Point3D(1385, 1490, 10));
        gold.PutInContainer(chest.Id, new Point2D(20, 20));
        deep.PutInContainer(gold.Id, new Point2D(20, 20));
        _items.Add([chest, gold, deep]);
        _targets.Result = TargetResult.ForObject(chest.Id);

        var context = await RunAsync();

        Assert.Equal("Removed 0x40000010.", Assert.Single(context.Output).Text);
        Assert.All(new[] { chest, gold, deep }, item => Assert.False(_items.TryGet(item.Id, out _)));
        Assert.Equal(["Disappeared 1073741840"], _view.Calls);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_AnItemThatDoesNotLieOnTheGround_IsLeftWhereItIs()
    {
        var backpack = Item(0x40000020);
        var dagger = Item(0x40000021);
        backpack.Equip(new Serial(2), LayerType.Backpack);
        dagger.PutInContainer(backpack.Id, new Point2D(20, 20));
        _items.Add([backpack, dagger]);

        _targets.Result = TargetResult.ForObject(dagger.Id);
        var carried = await RunAsync();
        _targets.Result = TargetResult.ForObject(backpack.Id);
        var worn = await RunAsync();
        _targets.Result = TargetResult.ForObject(new Serial(0x40000099));
        var unknown = await RunAsync();

        Assert.All(
            new[] { carried, worn, unknown },
            context => Assert.Equal("Only an item lying on the ground can be removed.", Assert.Single(context.Output).Text)
        );
        Assert.True(_items.TryGet(dagger.Id, out _));
        Assert.True(_items.TryGet(backpack.Id, out _));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ALocation_SaysCanceled()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0));

        var context = await RunAsync();

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_FromTheConsole_IsRefused()
    {
        var context = new CommandContext("remove", "remove", [], CommandSourceType.Console, null);

        await new RemoveCommand(_npcs, _targets, _items, _view, new StubGameLoop()).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_Texts_AreInTheServerLanguage()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0));

        var context = await RunAsync(TestLocalization.With((30008, "Bersaglio annullato.")));

        Assert.Equal("Bersaglio annullato.", Assert.Single(context.Output).Text);
    }

    private static ItemEntity Item(uint serial)
    {
        return new() { Id = new Serial(serial), TemplateId = "item", ItemId = 0x0E43, Amount = 1 };
    }

    private async Task<CommandContext> RunAsync(ILocalizationService? localization = null)
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }

        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".remove", "remove", [], CommandSourceType.InGame, session);

        await new RemoveCommand(_npcs, _targets, _items, _view, new StubGameLoop(), localization).ExecuteAsync(context);

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
