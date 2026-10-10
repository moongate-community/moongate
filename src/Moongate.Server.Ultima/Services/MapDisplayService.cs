using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.MapItems;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Shows map items with the map packets (0xF5 or 0x90, then 0x56) and keeps the course players plot on them in the
///     item's props, in pixels of the drawing.
/// </summary>
public sealed class MapDisplayService : IMapDisplayService
{
    // How far a map on the ground may be from the player that changes its course, in tiles.
    private const int ReachRange = 2;

    // The last facet a client older than 7.0.13 can draw: Trammel.
    private const int LastOldFacet = 1;

    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public MapDisplayService(
        IItemService items,
        IItemTemplateService templates,
        IMobileService mobiles,
        IPacketSendService sender
    )
    {
        _items = items;
        _templates = templates;
        _mobiles = mobiles;
        _sender = sender;
    }

    public bool Display(GameSession session, ItemEntity map)
    {
        if (!TryGetArea(map, out var area))
        {
            return false;
        }

        var old = session.ClientVersion is { } version && version < ClientVersion.Version70130;

        if (old && area.Facet > LastOldFacet)
        {
            return false;
        }

        var serial = map.Id.Value;
        _sender.TrySend(session.SessionId, old ? new OldMapDetailsPacket(serial, area) : new MapDetailsPacket(serial, area));
        _sender.TrySend(session.SessionId, new MapCommandPacket(serial, MapCommandType.ClearPins, false, 0, 0));

        foreach (var (x, y) in MapItemProps.GetPins(map))
        {
            _sender.TrySend(session.SessionId, new MapCommandPacket(serial, MapCommandType.AddPin, false, x, y));
        }

        var editable = MapItemProps.IsEditable(map) && TryGetPlayer(session, out var player) && CanEdit(player, map);
        _sender.TrySend(session.SessionId, new MapCommandPacket(serial, MapCommandType.EditableAnswer, editable, 0, 0));

        return true;
    }

    public void Handle(GameSession session, MapCommandRequestPacket packet)
    {
        if (!_items.TryGet(new Serial(packet.Serial), out var map) ||
            !TryGetArea(map, out var area) ||
            !TryGetPlayer(session, out var player) ||
            !CanEdit(player, map))
        {
            return;
        }

        if (packet.Command == MapCommandType.ToggleEditable)
        {
            var editable = !MapItemProps.IsEditable(map);
            MapItemProps.SetEditable(map, editable);
            _sender.TrySend(session.SessionId, new MapCommandPacket(map.Id.Value, MapCommandType.EditableAnswer, editable, 0, 0));

            return;
        }

        if (!MapItemProps.IsEditable(map))
        {
            return;
        }

        var pins = MapItemProps.GetPins(map).ToList();
        var inside = packet.X >= 0 && packet.Y >= 0 && packet.X < area.Width && packet.Y < area.Height;
        var known = packet.Number < pins.Count;

        switch (packet.Command)
        {
            case MapCommandType.AddPin when inside && pins.Count < MapItemProps.MaxPins:
                pins.Add((packet.X, packet.Y));

                break;
            case MapCommandType.InsertPin when inside && known && pins.Count < MapItemProps.MaxPins:
                pins.Insert(packet.Number, (packet.X, packet.Y));

                break;
            case MapCommandType.ChangePin when inside && known:
                pins[packet.Number] = (packet.X, packet.Y);

                break;
            case MapCommandType.RemovePin when known:
                pins.RemoveAt(packet.Number);

                break;
            case MapCommandType.ClearPins:
                pins.Clear();

                break;
            default:
                return;
        }

        MapItemProps.SetPins(map, pins);
    }

    private bool TryGetArea(ItemEntity map, out MapArea area)
    {
        _templates.TryGet(map.TemplateId, out var template);

        return MapItemProps.TryGetArea(map, template, out area);
    }

    private bool TryGetPlayer(GameSession session, out MobileEntity player)
    {
        // Safe: only read when the method returns true.
        player = null!;

        return session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out player!);
    }

    // In the player's own backpack, or on the ground within reach; never a protected or a fixed map.
    private bool CanEdit(MobileEntity player, ItemEntity map)
    {
        if (MapItemProps.IsProtected(map) || map.Movable == false)
        {
            return false;
        }

        return _items.GetOwner(map) is { } owner ? owner == player.Id : _items.CanReach(player, map, ReachRange);
    }
}
