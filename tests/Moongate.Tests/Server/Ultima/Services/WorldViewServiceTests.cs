using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Ultima.Types;
using Serilog;
using Serilog.Events;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class WorldViewServiceTests
{
    private const long AriaSession = 10;
    private const long BorisSession = 20;

    private readonly StubPacketSendService _sender = new();
    private readonly WorldConfig _world = new();
    private readonly MobileService _mobiles;
    private readonly ItemService _items;
    private readonly CapturingLogSink _log = new();
    private readonly WorldViewService _view;

    public WorldViewServiceTests()
    {
        var sectors = TestSectors.Create(_world);
        _items = TestItems.Create(sectors);
        _mobiles = new(new StubMovementService(), sectors);
        // The tooltip revisions (0xDC) are checked on their own below.
        _view = new(
            sectors,
            _mobiles,
            _items,
            _sender.Ignore<PropertyListInfoPacket>(),
            TestTooltips.Create(_items, _mobiles),
            _world,
            new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(_log).CreateLogger()
        );
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
    public void WornItemChanged_ShowsItOnTheWearerToEveryoneInRange()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        Enter(4, 3000, 3000, 30);
        var shirt = Worn(0x40000010, aria);
        ClearSent();

        _view.WornItemChanged(aria, shirt);

        Assert.Equal([AriaSession, BorisSession], _sender.SentSessionIds.Order());
        Assert.All(_sender.Sent, packet => Assert.Equal(shirt.Id, Assert.IsType<WornItemPacket>(packet).Item));
    }

    [Fact]
    public void WornItemRemoved_TakesItOffTheWearerForTheOthersInRange()
    {
        var aria = Enter(2, 1496, 1628, AriaSession);
        Enter(3, 1500, 1628, BorisSession);
        Enter(4, 3000, 3000, 30);
        var shirt = Worn(0x40000010, aria);
        ClearSent();

        _view.WornItemRemoved(aria, shirt);

        // The wearer's client already took it off when it was picked up.
        Assert.Equal([BorisSession], _sender.SentSessionIds);
        Assert.Equal(shirt.Id, Assert.IsType<RemoveEntityPacket>(Assert.Single(_sender.Sent)).Serial);
    }

    [Fact]
    public void Entered_FollowsEachMobileAndItemShownWithItsTooltipRevision()
    {
        var sender = new StubPacketSendService();
        var sectors = TestSectors.Create(_world);
        var items = TestItems.Create(sectors);
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var view = new WorldViewService(sectors, mobiles, items, sender, TestTooltips.Create(items, mobiles), _world);
        var boris = Mobile(3, 1500, 1628);
        var shirt = new ItemEntity { Id = new(0x40000010), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
        shirt.Equip(boris.Id, LayerType.Shirt);
        var gold = new ItemEntity { Id = new(0x40000050), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        items.Add([shirt, gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1500, 1629, 0));
        mobiles.EnterWorld(boris);
        var aria = Mobile(2, 1496, 1628);
        mobiles.EnterWorld(aria);

        view.Entered(aria, AriaSession, null);

        Assert.Equal(
            [typeof(MobileIncomingPacket), typeof(PropertyListInfoPacket), typeof(PropertyListInfoPacket), typeof(WorldItemSaPacket), typeof(PropertyListInfoPacket)],
            sender.Sent.Select(packet => packet.GetType())
        );
        Assert.Equal(
            [boris.Id, shirt.Id, gold.Id],
            sender.Sent.OfType<PropertyListInfoPacket>().Select(info => info.Serial)
        );
    }

    [Fact]
    public void WornItemChanged_FollowsTheItemWithItsTooltipRevision()
    {
        var sender = new StubPacketSendService();
        var sectors = TestSectors.Create(_world);
        var items = TestItems.Create(sectors);
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var view = new WorldViewService(sectors, mobiles, items, sender, TestTooltips.Create(items, mobiles), _world);
        var aria = Mobile(2, 1496, 1628);
        mobiles.EnterWorld(aria);
        view.Entered(aria, AriaSession, null);
        var shirt = Worn(0x40000010, aria);
        sender.Sent.Clear();

        view.WornItemChanged(aria, shirt);

        Assert.Equal([typeof(WornItemPacket), typeof(PropertyListInfoPacket)], sender.Sent.Select(packet => packet.GetType()));
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

    [Fact]
    public void Entered_UsesTheConfiguredViewRange()
    {
        _world.ViewRange = 5;
        Enter(3, 1502, 1628, BorisSession);
        Enter(4, 1501, 1628, 30);
        ClearSent();

        Enter(2, 1496, 1628, AriaSession);

        Assert.Equal([AriaSession, 30L], _sender.SentSessionIds.Distinct().Order());
    }

    [Fact]
    public void Entered_LogsAtDebugWhatWasSentFromItsSector()
    {
        Ground(0x40000001, 1510, 1628);
        _mobiles.EnterWorld(Mobile(5, 1505, 1628));
        var boris = Mobile(3, 1500, 1628);
        boris.AccountId = new Serial(0x42);
        _mobiles.EnterWorld(boris);

        Enter(2, 1503, 1628, AriaSession);

        var entry = Assert.Single(_log.Events);
        Assert.Equal(LogEventLevel.Debug, entry.Level);
        Assert.Equal(
            "\"M2\" entered sector (93, 101) of Trammel: sent 1 items, 1 NPCs and 1 players",
            entry.RenderMessage()
        );
    }

    [Fact]
    public void Moved_IntoAnotherSector_LogsTheNewcomersSent_AndWithinOneLogsNothing()
    {
        var aria = Enter(2, 1502, 1628, AriaSession);
        Ground(0x40000001, 1522, 1628);
        _mobiles.EnterWorld(Mobile(5, 1522, 1630));

        Step(aria, 1503, 1628, false);
        Assert.Single(_log.Events);

        Step(aria, 1504, 1628, false);

        Assert.Equal(2, _log.Events.Count);
        Assert.Equal(
            "\"M2\" entered sector (94, 101) of Trammel: sent 1 items, 1 NPCs and 0 players",
            _log.Events[1].RenderMessage()
        );
    }

    [Fact]
    public void Entered_AnItemWithALight_IsSentWithItsShape()
    {
        var candle = Ground(0x40000001, 1500, 1628);
        candle.Props = new() { ["light"] = "circle150" };
        Ground(0x40000002, 1501, 1628).Props = new() { ["light"] = "no such light" };

        Enter(2, 1496, 1628, AriaSession);

        Assert.Equal([2, 0], _sender.Sent.OfType<WorldItemSaPacket>().Select(packet => packet.Light));
    }

    private ItemEntity Ground(uint serial, int x, int y)
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, MapType.Trammel, new Point3D(x, y, 0));

        return item;
    }

    private ItemEntity Worn(uint serial, MobileEntity wearer)
    {
        var item = new ItemEntity { Id = new(serial), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
        item.Equip(wearer.Id, LayerType.Shirt);
        _items.Add([item]);

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
