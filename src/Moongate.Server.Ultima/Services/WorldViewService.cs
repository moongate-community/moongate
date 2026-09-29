using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Sends the players in range of a mobile what changed about it, keeping the character → session map itself because
///     <c>ISessionService.TryGetByCharacterId</c> scans every session.
/// </summary>
public sealed class WorldViewService : IWorldViewService
{

    private static readonly ClientVersion StygianAbyss = new(7, 0, 0, 0);
    private static readonly ClientVersion HighSeas = new(7, 0, 9, 0);

    private readonly Dictionary<Serial, Viewer> _sessions = [];
    private readonly ISectorService _sectors;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IPacketSendService _sender;
    private readonly WorldConfig _world;

    // Read on every use: the configured range of the live world (ultima.world.view_range).
    private int ViewRange => _world.ViewRange;

    public WorldViewService(
        ISectorService sectors,
        IMobileService mobiles,
        IItemService items,
        IPacketSendService sender,
        WorldConfig world
    )
    {
        _sectors = sectors;
        _mobiles = mobiles;
        _items = items;
        _sender = sender;
        _world = world;
    }

    public void Entered(MobileEntity mobile, long sessionId, ClientVersion? version)
    {
        _sessions[mobile.Id] = new(sessionId, version);
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            _sender.TrySend(sessionId, Incoming(other));

            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, incoming ??= Incoming(mobile));
            }
        }

        foreach (var item in _sectors.GetItemsInRange(mobile.Map, mobile.Location, ViewRange))
        {
            _sender.TrySend(sessionId, WorldItem(item, version));
        }
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation, bool running)
    {
        // Players that saw the old tile but not the new one lose the mover.
        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, oldLocation, ViewRange))
        {
            if (other.Id != mobile.Id &&
                !InRange(other.Location, mobile.Location) &&
                _sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, new RemoveEntityPacket(mobile.Id));
            }
        }

        var hasSession = _sessions.TryGetValue(mobile.Id, out var own);
        MobileMovingPacket? moving = null;
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            var sawIt = InRange(other.Location, oldLocation);

            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(
                    viewer.SessionId,
                    sawIt ? moving ??= Moving(mobile, running) : incoming ??= Incoming(mobile)
                );
            }

            // The mover's client drops what it walks away from by itself, as in ModernUO; it only needs the newcomers.
            if (!sawIt && hasSession)
            {
                _sender.TrySend(own!.SessionId, Incoming(other));
            }
        }

        if (!hasSession)
        {
            return;
        }

        foreach (var item in _sectors.GetItemsInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (item.GroundLocation is { } spot && !InRange(spot, oldLocation))
            {
                _sender.TrySend(own!.SessionId, WorldItem(item, own.Version));
            }
        }
    }

    public void Left(MobileEntity mobile)
    {
        // Told even when it never registered: others may have seen it between the two login passes on the loop.
        _sessions.Remove(mobile.Id);
        var remove = new RemoveEntityPacket(mobile.Id);

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, remove);
            }
        }
    }

    public void MobileAppeared(MobileEntity mobile)
    {
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, incoming ??= Incoming(mobile));
            }
        }
    }

    public void ItemAppeared(ItemEntity item)
    {
        if (item.Map is not { } map || item.GroundLocation is not { } spot)
        {
            return;
        }

        foreach (var other in _sectors.GetMobilesInRange(map, spot, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, WorldItem(item, viewer.Version));
            }
        }
    }

    public void ShowItemTo(MobileEntity viewer, ItemEntity item)
    {
        if (item.GroundLocation is not null && _sessions.TryGetValue(viewer.Id, out var session))
        {
            _sender.TrySend(session.SessionId, WorldItem(item, session.Version));
        }
    }

    public void ItemDisappeared(ItemEntity item)
    {
        if (item.Map is not { } map || item.GroundLocation is not { } spot)
        {
            return;
        }

        var remove = new RemoveEntityPacket(item.Id);

        foreach (var other in _sectors.GetMobilesInRange(map, spot, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, remove);
            }
        }
    }

    private static IOutgoingPacket WorldItem(ItemEntity item, ClientVersion? version)
    {
        var spot = item.GroundLocation!.Value;

        // As ModernUO: 0xF3 from 7.0.0.0 (Stygian Abyss), two bytes longer from 7.0.9.0 (High Seas); unknown is newest.
        if (version is null || version.CompareTo(StygianAbyss) >= 0)
        {
            var highSeas = version is null || version.CompareTo(HighSeas) >= 0;

            return new WorldItemSaPacket(item.Id, item.ItemId, item.Amount, spot, item.Hue, highSeas);
        }

        return new WorldItemPacket(item.Id, item.ItemId, item.Amount, spot, item.Hue);
    }

    private MobileIncomingPacket Incoming(MobileEntity mobile)
    {
        var worn = _items.GetWorn(mobile.Id);

        return new(
            mobile.Id,
            new Body((ushort)mobile.Body),
            mobile.Location,
            mobile.Direction,
            mobile.SkinHue,
            _mobiles.GetFlags(mobile),
            mobile.Notoriety ?? NotorietyType.Innocent,
            _mobiles.GetEquipment(mobile, worn)
        );
    }

    private MobileMovingPacket Moving(MobileEntity mobile, bool running)
    {
        return new(
            mobile.Id,
            new Body((ushort)mobile.Body),
            mobile.Location,
            mobile.Direction,
            running,
            mobile.SkinHue,
            _mobiles.GetFlags(mobile),
            mobile.Notoriety ?? NotorietyType.Innocent
        );
    }

    private bool InRange(Point3D a, Point3D b)
    {
        return Math.Abs(a.X - b.X) <= ViewRange && Math.Abs(a.Y - b.Y) <= ViewRange;
    }
}
