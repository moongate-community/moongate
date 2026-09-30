using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;
using Serilog;

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
    private readonly ITooltipService _tooltips;
    private readonly WorldConfig _world;
    private readonly ILogger _logger;

    // Read on every use: the configured range of the live world (ultima.world.view_range).
    private int ViewRange => _world.ViewRange;

    public WorldViewService(
        ISectorService sectors,
        IMobileService mobiles,
        IItemService items,
        IPacketSendService sender,
        ITooltipService tooltips,
        WorldConfig world,
        ILogger? logger = null
    )
    {
        _logger = logger ?? Log.ForContext<WorldViewService>();
        _tooltips = tooltips;
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
        var sent = new SentCounts();

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            SendMobile(sessionId, other, Incoming(other));
            sent.Add(other);

            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
            }
        }

        foreach (var item in _sectors.GetItemsInRange(mobile.Map, mobile.Location, ViewRange))
        {
            SendItem(sessionId, item, version);
            sent.Items++;
        }

        LogSectorEntry(mobile, sent);
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
        var sent = new SentCounts();
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
                if (sawIt)
                {
                    _sender.TrySend(viewer.SessionId, moving ??= Moving(mobile, running));
                }
                else
                {
                    SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
                }
            }

            // The mover's client drops what it walks away from by itself, as in ModernUO; it only needs the newcomers.
            if (!sawIt && hasSession)
            {
                SendMobile(own!.SessionId, other, Incoming(other));
                sent.Add(other);
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
                SendItem(own!.SessionId, item, own.Version);
                sent.Items++;
            }
        }

        if (SectorOf(mobile.Location) != SectorOf(oldLocation))
        {
            LogSectorEntry(mobile, sent);
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
                SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
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
                SendItem(viewer.SessionId, item, viewer.Version);
            }
        }
    }

    public void ShowItemTo(MobileEntity viewer, ItemEntity item)
    {
        if (item.GroundLocation is not null && _sessions.TryGetValue(viewer.Id, out var session))
        {
            SendItem(session.SessionId, item, session.Version);
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

    public void WornItemChanged(MobileEntity wearer, ItemEntity item)
    {
        var worn = new WornItemPacket(item);
        var info = _tooltips.Info(item);

        foreach (var other in _sectors.GetMobilesInRange(wearer.Map, wearer.Location, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, worn);
                _sender.TrySend(viewer.SessionId, info);
            }
        }
    }

    public void WornItemRemoved(MobileEntity wearer, ItemEntity item)
    {
        var remove = new RemoveEntityPacket(item.Id);

        foreach (var other in _sectors.GetMobilesInRange(wearer.Map, wearer.Location, ViewRange))
        {
            if (other.Id != wearer.Id && _sessions.TryGetValue(other.Id, out var viewer))
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

    private static (int X, int Y) SectorOf(Point3D location)
    {
        return (location.X / SectorService.SectorSize, location.Y / SectorService.SectorSize);
    }

    private void LogSectorEntry(MobileEntity mobile, SentCounts sent)
    {
        var (x, y) = SectorOf(mobile.Location);
        _logger.Debug(
            "{Name} entered sector ({SectorX}, {SectorY}) of {Map}: sent {Items} items, {Npcs} NPCs and {Players} players",
            mobile.Name,
            x,
            y,
            mobile.Map,
            sent.Items,
            sent.Npcs,
            sent.Players
        );
    }

    // The mobile, then the revision of its tooltip and of each worn item's, as ModernUO: the client asks for the
    // tooltips it does not have yet.
    private void SendMobile(long sessionId, MobileEntity mobile, MobileIncomingPacket incoming)
    {
        _sender.TrySend(sessionId, incoming);
        _sender.TrySend(sessionId, _tooltips.Info(mobile));

        foreach (var item in _items.GetWorn(mobile.Id))
        {
            _sender.TrySend(sessionId, _tooltips.Info(item));
        }
    }

    private void SendItem(long sessionId, ItemEntity item, ClientVersion? version)
    {
        _sender.TrySend(sessionId, WorldItem(item, version));
        _sender.TrySend(sessionId, _tooltips.Info(item));
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
