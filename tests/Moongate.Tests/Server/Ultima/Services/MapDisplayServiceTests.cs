using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.MapItems;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MapDisplayServiceTests : IAsyncDisposable
{
    private static readonly MapArea Area = new(1000, 1000, 1200, 1200, 200, 200, 1);

    private readonly StubPacketSendService _sender = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly MobileService _mobiles;
    private readonly ItemService _items;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With<ItemTemplate>(new ItemTemplate { Id = "map", ItemId = new Serial(0x14EC) })
    );

    private readonly MobileEntity _aria = new()
    {
        Id = new(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1100, 1100, 0)
    };

    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _map = new() { Id = new Serial(0x40000002), TemplateId = "map", ItemId = 0x14EC, Amount = 1 };

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private MapDisplayService _maps = null!;

    public MapDisplayServiceTests()
    {
        _mobiles = new(new StubMovementService(), _sectors);
        _items = TestItems.Create(_sectors);
    }

    [Fact]
    public async Task Display_SendsTheDetails_TheMap_EveryPin_AndWhetherItMayBeChanged()
    {
        await StartAsync();
        MapItemProps.SetPins(_map, [(10, 20), (30, 40)]);
        var shown = false;

        await OnLoopAsync(() => shown = _maps.Display(_session, _map));

        Assert.True(shown);
        Assert.Equal(
            ["F5", "56:5:False:0:0", "56:1:False:10:20", "56:1:False:30:40", "56:7:False:0:0"],
            _sender.Sent.Select(Describe)
        );
    }

    [Fact]
    public async Task Display_ToAnOldClient_SendsTheOldDetails_ButNoMapOfAnotherFacet()
    {
        await StartAsync();
        _session.NetworkSession.SetClientVersion(ClientVersion.Parse("6.0.14.2"));

        await OnLoopAsync(() => _maps.Display(_session, _map));
        Assert.Equal("90", Describe(_sender.Sent[0]));

        _sender.Sent.Clear();
        MapItemProps.SetArea(_map, Area with { Facet = 3 });
        var shown = true;
        await OnLoopAsync(() => shown = _maps.Display(_session, _map));

        Assert.False(shown);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Display_OfABlankMap_SendsNothing()
    {
        await StartAsync();
        _map.Props = null;
        var shown = true;

        await OnLoopAsync(() => shown = _maps.Display(_session, _map));

        Assert.False(shown);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task ToggleEditable_OpensTheCourse_AndAnswersSo()
    {
        await StartAsync();

        await HandleAsync(MapCommandType.ToggleEditable);

        Assert.True(MapItemProps.IsEditable(_map));
        Assert.Equal(["56:7:True:0:0"], _sender.Sent.Select(Describe));
    }

    [Fact]
    public async Task APin_IsAddedOnlyWhileTheCourseMayBeChanged_AndInsideTheDrawing()
    {
        await StartAsync();

        await HandleAsync(MapCommandType.AddPin, 0, 10, 20);
        Assert.Empty(MapItemProps.GetPins(_map));

        MapItemProps.SetEditable(_map, true);
        await HandleAsync(MapCommandType.AddPin, 0, 10, 20);
        await HandleAsync(MapCommandType.AddPin, 0, 200, 20);
        await HandleAsync(MapCommandType.AddPin, 0, -1, 20);

        Assert.Equal([(10, 20)], MapItemProps.GetPins(_map));
    }

    [Fact]
    public async Task ACourse_HoldsAtMostFiftyPins()
    {
        await StartAsync();
        MapItemProps.SetEditable(_map, true);
        MapItemProps.SetPins(_map, Enumerable.Range(0, 50).Select(i => (i, i)).ToList());

        await HandleAsync(MapCommandType.AddPin, 0, 60, 60);
        await HandleAsync(MapCommandType.InsertPin, 0, 60, 60);

        Assert.Equal(50, MapItemProps.GetPins(_map).Count);
    }

    [Fact]
    public async Task InsertChangeRemoveAndClear_WorkOnTheCourse_AndAnIndexPastItIsIgnored()
    {
        await StartAsync();
        MapItemProps.SetEditable(_map, true);
        MapItemProps.SetPins(_map, [(1, 1), (2, 2)]);

        await HandleAsync(MapCommandType.InsertPin, 1, 5, 5);
        Assert.Equal([(1, 1), (5, 5), (2, 2)], MapItemProps.GetPins(_map));

        await HandleAsync(MapCommandType.ChangePin, 0, 7, 7);
        await HandleAsync(MapCommandType.ChangePin, 9, 8, 8);
        await HandleAsync(MapCommandType.InsertPin, 9, 8, 8);
        await HandleAsync(MapCommandType.RemovePin, 9);
        Assert.Equal([(7, 7), (5, 5), (2, 2)], MapItemProps.GetPins(_map));

        await HandleAsync(MapCommandType.RemovePin, 1);
        Assert.Equal([(7, 7), (2, 2)], MapItemProps.GetPins(_map));

        await HandleAsync(MapCommandType.ClearPins);
        Assert.Empty(MapItemProps.GetPins(_map));
    }

    [Fact]
    public async Task AProtectedMap_OrOneOutOfReach_IsNotChanged()
    {
        await StartAsync();
        MapItemProps.SetProtected(_map, true);

        await HandleAsync(MapCommandType.ToggleEditable);
        Assert.False(MapItemProps.IsEditable(_map));

        MapItemProps.SetProtected(_map, false);
        _items.PlaceOnGround(_map, MapType.Trammel, new Point3D(1103, 1100, 0));
        await HandleAsync(MapCommandType.ToggleEditable);

        Assert.False(MapItemProps.IsEditable(_map));
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task AMapInAnotherPlayersBackpack_IsNotChanged()
    {
        await StartAsync();
        _backpack.Equip(new Serial(3), LayerType.Backpack);

        await HandleAsync(MapCommandType.ToggleEditable);

        Assert.False(MapItemProps.IsEditable(_map));
    }

    private static string Describe(IOutgoingPacket packet)
    {
        return packet switch
        {
            MapDetailsPacket => "F5",
            OldMapDetailsPacket => "90",
            MapCommandPacket command => $"56:{(int)command.Command}:{command.Flag}:{command.X}:{command.Y}",
            _ => packet.GetType().Name
        };
    }

    private Task HandleAsync(MapCommandType command, int number = 0, int x = 0, int y = 0)
    {
        var packet = new MapCommandRequestPacket { Serial = _map.Id.Value, Command = command, Number = number, X = x, Y = y };

        return OnLoopAsync(() => _maps.Handle(_session, packet));
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, _aria.Id));
        _mobiles.EnterWorld(_aria);
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _map.PutInContainer(_backpack.Id, new Point2D(10, 10));
        _items.Add([_backpack, _map]);
        MapItemProps.SetArea(_map, Area);
        _maps = new(_items, _templates, _mobiles, _sender);
    }

    private Task OnLoopAsync(Action action)
    {
        return _fixture.ExecuteOnLoopAsync(action);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
