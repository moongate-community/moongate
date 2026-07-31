using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types.World;
using Moongate.UO.Data.Mobiles;

namespace Moongate.Server.Services.World;

/// <summary>
/// Holds what each client has been told exists and sends the difference. The known set is the whole
/// point: a client cannot be told something appeared without knowing what it already has.
/// </summary>
public sealed class VisibilityService : IVisibilityService
{
    private readonly ISpatialIndexService _spatial;
    private readonly IItemService _items;
    private readonly IVirtualSerialService _virtualSerials;
    private readonly Dictionary<long, HashSet<Serial>> _known = [];

    public VisibilityService(ISpatialIndexService spatial, IItemService items, IVirtualSerialService virtualSerials)
    {
        _spatial = spatial;
        _items = items;
        _virtualSerials = virtualSerials;
    }

    public VisibilityDelta Refresh(PlayerSession session)
    {
        if (session.Character is not { } character)
        {
            return new([], []);
        }

        var range = session.ViewRange;
        var mobiles = _spatial.GetMobilesInRange(character.MapId, character.Position, range)
                              .Where(mobile => mobile.Id != character.Id)
                              .ToList();
        var items = _spatial.GetItemsInRange(character.MapId, character.Position, range);

        var visible = new HashSet<Serial>(mobiles.Count + items.Count);
        var known = KnownSet(session);
        var entered = new List<Serial>();

        foreach (var mobile in mobiles)
        {
            visible.Add(mobile.Id);

            if (known.Add(mobile.Id))
            {
                entered.Add(mobile.Id);
                session.Send(Incoming(mobile));
            }
        }

        foreach (var item in items)
        {
            visible.Add(item.Id);

            if (known.Add(item.Id))
            {
                entered.Add(item.Id);
                session.Send(Incoming(item));
            }
        }

        var left = known.Where(serial => !visible.Contains(serial)).ToList();

        foreach (var serial in left)
        {
            known.Remove(serial);
            session.Send(new DeleteObjectPacket(serial));
        }

        return new(entered, left);
    }

    public VisibilityChangeType UpdateFor(PlayerSession session, MobileEntity mobile)
    {
        if (session.Character is not { } character || mobile.Id == character.Id)
        {
            return VisibilityChangeType.None;
        }

        return Apply(
            session,
            mobile.Id,
            InRange(character, session, mobile.MapId, mobile.Position),
            () => Incoming(mobile),
            () => new UpdatePlayerPacket(
                mobile.Id,
                (ushort)mobile.Body,
                (ushort)mobile.Position.X,
                (ushort)mobile.Position.Y,
                (sbyte)mobile.Position.Z,
                mobile.Direction,
                mobile.SkinHue,
                MobileDrawing.BuildFlags(mobile),
                Notoriety.Resolve(mobile.Kills, mobile.Criminal)
            )
        );
    }

    public VisibilityChangeType UpdateFor(PlayerSession session, ItemEntity item)
    {
        if (session.Character is not { } character)
        {
            return VisibilityChangeType.None;
        }

        // An item has no separate "it moved" packet: redrawing it is how it moves.
        return Apply(
            session,
            item.Id,
            InRange(character, session, item.MapId, item.Position),
            () => Incoming(item),
            () => Incoming(item)
        );
    }

    public int Undraw(Serial serial)
    {
        var count = 0;

        foreach (var known in _known.Values)
        {
            if (known.Remove(serial))
            {
                count++;
            }
        }

        return count;
    }

    public void Forget(PlayerSession session)
        => _known.Remove(session.SessionId);

    public IReadOnlySet<Serial> KnownTo(PlayerSession session)
        => _known.TryGetValue(session.SessionId, out var known) ? known : new HashSet<Serial>();

    /// <summary>
    /// The three-row table: in range and unknown means draw, out of range and known means undraw,
    /// known and still in range means move. Anything else is not news.
    /// </summary>
    private VisibilityChangeType Apply<TDraw, TMove>(
        PlayerSession session,
        Serial serial,
        bool inRange,
        Func<TDraw> draw,
        Func<TMove> move
    )
        where TDraw : IOutgoingPacket
        where TMove : IOutgoingPacket
    {
        var known = KnownSet(session);

        if (inRange)
        {
            if (known.Add(serial))
            {
                session.Send(draw());

                return VisibilityChangeType.Drawn;
            }

            session.Send(move());

            return VisibilityChangeType.Moved;
        }

        if (!known.Remove(serial))
        {
            return VisibilityChangeType.None;
        }

        session.Send(new DeleteObjectPacket(serial));

        return VisibilityChangeType.Undrawn;
    }

    private static bool InRange(MobileEntity character, PlayerSession session, int mapId, Point3D position)
        => character.MapId == mapId && character.Position.InRange(position, session.ViewRange);

    private MobileIncomingPacket Incoming(MobileEntity mobile)
        => new(
            mobile.Id,
            (ushort)mobile.Body,
            (ushort)mobile.Position.X,
            (ushort)mobile.Position.Y,
            (sbyte)mobile.Position.Z,
            mobile.Direction,
            mobile.SkinHue,
            MobileDrawing.BuildFlags(mobile),
            Notoriety.Resolve(mobile.Kills, mobile.Criminal),
            MobileDrawing.BuildEquipment(mobile, _items, _virtualSerials)
        );

    private static WorldItemPacket Incoming(ItemEntity item)
        => new(item.Id, (ushort)item.ItemId, (ushort)item.Amount, item.Position, item.Hue);

    private HashSet<Serial> KnownSet(PlayerSession session)
    {
        if (_known.TryGetValue(session.SessionId, out var known))
        {
            return known;
        }

        known = [];
        _known[session.SessionId] = known;

        return known;
    }
}
