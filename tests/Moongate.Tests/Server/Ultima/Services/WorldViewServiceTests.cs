using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class WorldViewServiceTests
{
    private const long AriaSession = 10;
    private const long BorisSession = 20;

    private readonly StubPacketSendService _sender = new();
    private readonly MobileService _mobiles;
    private readonly ItemService _items;
    private readonly WorldViewService _view;

    public WorldViewServiceTests()
    {
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors);
        _mobiles = new(new StubMovementService(), sectors);
        _view = new(sectors, _mobiles, _items, _sender);
    }

    [Fact]
    public void Entered_ShowsBothPlayersToEachOther()
    {
        var boris = Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        var aria = Enter(2, 1496, 1628, AriaSession);

        Assert.Equal(boris.Id, IncomingTo(AriaSession));
        Assert.Equal(aria.Id, IncomingTo(BorisSession));
    }

    [Fact]
    public void Entered_FarAway_SendsNothing()
    {
        Enter(3, 3000, 3000, BorisSession);
        ClearSent();

        Enter(2, 1496, 1628, AriaSession);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void Moved_WithinRange_SendsMovingToTheOther()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        var moving = Step(aria, 1497, 1628, false);

        var packet = Assert.IsType<MobileMovingPacket>(Assert.Single(moving));
        Assert.Equal((aria.Id, new Point3D(1497, 1628, 0), false), (packet.Serial, packet.Location, packet.Running));
        Assert.Equal([BorisSession], _sender.SentSessionIds);
    }

    [Fact]
    public void Moved_Running_MarksTheRun()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        var packet = Assert.IsType<MobileMovingPacket>(Assert.Single(Step(aria, 1497, 1628, true)));

        Assert.True(packet.Running);
    }

    [Fact]
    public void Moved_ATurnOnTheSpot_SendsMovingWithTheNewDirection()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        aria.Direction = DirectionType.North;
        _view.Moved(aria, aria.Location, false);

        var packet = Assert.IsType<MobileMovingPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(DirectionType.North, packet.Direction);
    }

    [Fact]
    public void Moved_IntoRange_ShowsBothPlayersToEachOther()
    {
        // Aria steps from 19 to 18 tiles west of Boris.
        var aria = Enter(2, 1481, 1628, AriaSession);
        var boris = Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        Step(aria, 1482, 1628, false);

        Assert.Equal(aria.Id, IncomingTo(BorisSession));
        Assert.Equal(boris.Id, IncomingTo(AriaSession));
        Assert.DoesNotContain(_sender.Sent, packet => packet is MobileMovingPacket);
    }

    [Fact]
    public void Moved_OutOfRange_RemovesTheMoverFromTheOther()
    {
        var aria = Enter(2, 1482, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        Step(aria, 1481, 1628, false);

        var remove = Assert.IsType<RemoveEntityPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(aria.Id, remove.Serial);
        Assert.Equal([BorisSession], _sender.SentSessionIds);
    }

    [Fact]
    public void Left_RemovesThePlayerFromThoseInRange()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        _view.Left(aria);

        var remove = Assert.IsType<RemoveEntityPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(aria.Id, remove.Serial);
        Assert.Equal([BorisSession], _sender.SentSessionIds);
    }

    [Fact]
    public void Left_ThePlayerNoLongerReceivesPackets()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        var boris = Enter(3, 1500, 1628, BorisSession);
        _view.Left(aria);
        _mobiles.LeaveWorld(aria.Id);
        ClearSent();

        Step(boris, 1499, 1628, false);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void Left_WithoutHavingEntered_StillRemovesItFromThoseInRange()
    {
        // In the grid but never registered: its session closed between the two login passes on the loop.
        var aria = Mobile(2, 1496, 1628);
        _mobiles.EnterWorld(aria);
        Enter(3, 1500, 1628, BorisSession);
        ClearSent();

        _view.Left(aria);

        var remove = Assert.IsType<RemoveEntityPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(aria.Id, remove.Serial);
        Assert.Equal([BorisSession], _sender.SentSessionIds);
    }

    [Fact]
    public void Left_AnUnknownMobile_SendsNothing()
    {
        _view.Left(Mobile(9, 1496, 1628));

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void Entered_ShowsTheOtherWithWhatItWears()
    {
        var boris = Mobile(3, 1500, 1628);
        var shirt = new ItemEntity { Id = new(0x40000010), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
        shirt.Equip(boris.Id, LayerType.Shirt);
        _items.Add([shirt]);
        _mobiles.EnterWorld(boris);
        _view.Entered(boris, BorisSession, null);
        ClearSent();

        Enter(2, 1496, 1628, AriaSession);

        var incoming = Assert.IsType<MobileIncomingPacket>(_sender.Sent[_sender.SentSessionIds.IndexOf(AriaSession)]);
        Assert.Contains(incoming.Equipment, entry => entry.Serial == shirt.Id);
    }

    [Fact]
    public void Entered_ShowsTheGroundItemsInRange()
    {
        var gold = Ground(0x40000050, 1500, 1628);

        Enter(2, 1496, 1628, AriaSession);

        var shown = Assert.IsType<WorldItemSaPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(gold.Id, shown.Serial);
        Assert.True(shown.HighSeas);
    }

    [Fact]
    public void Entered_AnOldClient_GetsTheOldPacket()
    {
        Ground(0x40000050, 1500, 1628);
        var aria = Mobile(2, 1496, 1628);
        _mobiles.EnterWorld(aria);

        _view.Entered(aria, AriaSession, new ClientVersion(6, 0, 14, 2));

        Assert.IsType<WorldItemPacket>(Assert.Single(_sender.Sent));
    }

    [Fact]
    public void Entered_AStygianAbyssClient_GetsTheShortSaPacket()
    {
        Ground(0x40000050, 1500, 1628);
        var aria = Mobile(2, 1496, 1628);
        _mobiles.EnterWorld(aria);

        _view.Entered(aria, AriaSession, new ClientVersion(7, 0, 5, 0));

        Assert.False(Assert.IsType<WorldItemSaPacket>(Assert.Single(_sender.Sent)).HighSeas);
    }

    [Fact]
    public void Moved_IntoRangeOfAGroundItem_ShowsItOnce()
    {
        var aria = Enter(2, 1481, 1628, AriaSession);
        var gold = Ground(0x40000050, 1500, 1628);
        ClearSent();

        Step(aria, 1482, 1628, false);
        Step(aria, 1483, 1628, false);

        var shown = Assert.IsType<WorldItemSaPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(gold.Id, shown.Serial);
    }

    [Fact]
    public void ItemAppeared_ShowsItToEveryoneInRange()
    {
        Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        Enter(4, 3000, 3000, 30);
        var gold = Ground(0x40000050, 1498, 1628);
        ClearSent();

        _view.ItemAppeared(gold);

        Assert.All(_sender.Sent, packet => Assert.Equal(gold.Id, Assert.IsType<WorldItemSaPacket>(packet).Serial));
        Assert.Equal([AriaSession, BorisSession], _sender.SentSessionIds.Order());
    }

    [Fact]
    public void ItemDisappeared_RemovesItFromEveryoneInRange()
    {
        Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        var gold = Ground(0x40000050, 1498, 1628);
        _items.Hide(gold);
        ClearSent();

        _view.ItemDisappeared(gold);

        Assert.All(_sender.Sent, packet => Assert.Equal(gold.Id, Assert.IsType<RemoveEntityPacket>(packet).Serial));
        Assert.Equal([AriaSession, BorisSession], _sender.SentSessionIds.Order());
    }

    [Fact]
    public void ShowItemTo_SendsItOnlyToThatPlayer()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        var gold = Ground(0x40000050, 1498, 1628);
        ClearSent();

        _view.ShowItemTo(aria, gold);

        Assert.Equal(gold.Id, Assert.IsType<WorldItemSaPacket>(Assert.Single(_sender.Sent)).Serial);
        Assert.Equal([AriaSession], _sender.SentSessionIds);
    }

    [Fact]
    public void MobileAppeared_ShowsItToThePlayersInRange()
    {
        Enter(2, 1496, 1628, AriaSession);
        Enter(3, 3000, 3000, BorisSession);
        var orc = Mobile(9, 1500, 1628);
        _mobiles.EnterWorld(orc);
        ClearSent();

        _view.MobileAppeared(orc);

        Assert.Equal(orc.Id, IncomingTo(AriaSession));
        Assert.Equal([AriaSession], _sender.SentSessionIds);
    }

    private ItemEntity Ground(uint serial, int x, int y)
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, MapType.Trammel, new Point3D(x, y, 0));

        return item;
    }

    private MobileEntity Enter(uint serial, int x, int y, long session)
    {
        var mobile = Mobile(serial, x, y);
        _mobiles.EnterWorld(mobile);
        _view.Entered(mobile, session, null);

        return mobile;
    }

    private List<IOutgoingPacket> Step(MobileEntity mobile, int x, int y, bool running)
    {
        var old = mobile.Location;
        mobile.Direction = x > old.X ? DirectionType.East : DirectionType.West;
        Assert.Equal(MoveResultType.Moved, _mobiles.TryMove(mobile, mobile.Direction));
        Assert.Equal(new Point3D(x, y, 0), mobile.Location);
        _view.Moved(mobile, old, running);

        return _sender.Sent;
    }

    private Serial IncomingTo(long session)
    {
        var index = _sender.SentSessionIds.IndexOf(session);

        return Assert.IsType<MobileIncomingPacket>(_sender.Sent[index]).Serial;
    }

    private void ClearSent()
    {
        _sender.Sent.Clear();
        _sender.SentSessionIds.Clear();
    }

    private static MobileEntity Mobile(uint serial, int x, int y)
    {
        return new()
        {
            Id = new Serial(serial), Name = $"M{serial}", Body = 0x0190, Map = MapType.Trammel,
            Location = new Point3D(x, y, 0), Direction = DirectionType.East
        };
    }
}
