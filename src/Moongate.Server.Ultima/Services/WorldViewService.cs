using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
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
    public const int ViewRange = 18;

    private readonly Dictionary<Serial, long> _sessions = [];
    private readonly ISectorService _sectors;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IPacketSendService _sender;

    public WorldViewService(ISectorService sectors, IMobileService mobiles, IItemService items, IPacketSendService sender)
    {
        _sectors = sectors;
        _mobiles = mobiles;
        _items = items;
        _sender = sender;
    }

    public void Entered(MobileEntity mobile, long sessionId)
    {
        _sessions[mobile.Id] = sessionId;
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            _sender.TrySend(sessionId, Incoming(other));

            if (_sessions.TryGetValue(other.Id, out var otherSession))
            {
                _sender.TrySend(otherSession, incoming ??= Incoming(mobile));
            }
        }
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation, bool running)
    {
        // Players that saw the old tile but not the new one lose the mover.
        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, oldLocation, ViewRange))
        {
            if (other.Id != mobile.Id &&
                !InRange(other.Location, mobile.Location) &&
                _sessions.TryGetValue(other.Id, out var otherSession))
            {
                _sender.TrySend(otherSession, new RemoveEntityPacket(mobile.Id));
            }
        }

        var hasSession = _sessions.TryGetValue(mobile.Id, out var ownSession);
        MobileMovingPacket? moving = null;
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            var sawIt = InRange(other.Location, oldLocation);

            if (_sessions.TryGetValue(other.Id, out var otherSession))
            {
                _sender.TrySend(otherSession, sawIt ? moving ??= Moving(mobile, running) : incoming ??= Incoming(mobile));
            }

            // The mover's client drops what it walks away from by itself, as in ModernUO; it only needs the newcomers.
            if (!sawIt && hasSession)
            {
                _sender.TrySend(ownSession, Incoming(other));
            }
        }
    }

    public void Left(MobileEntity mobile)
    {
        if (!_sessions.Remove(mobile.Id))
        {
            return;
        }

        var remove = new RemoveEntityPacket(mobile.Id);

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetValue(other.Id, out var otherSession))
            {
                _sender.TrySend(otherSession, remove);
            }
        }
    }

    private MobileIncomingPacket Incoming(MobileEntity mobile)
    {
        var worn = _items.GetOwnedBy(mobile.Id).Where(item => item.MobileId == mobile.Id);

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

    private static bool InRange(Point3D a, Point3D b)
    {
        return Math.Abs(a.X - b.X) <= ViewRange && Math.Abs(a.Y - b.Y) <= ViewRange;
    }
}
