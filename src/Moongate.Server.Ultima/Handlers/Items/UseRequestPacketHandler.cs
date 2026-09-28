using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     Answers a double click (0x06) on a container the session's character carries, such as its backpack or a bag in
///     it: the container's gump (0x24), then its items (0x3C). Anything else is not handled yet.
/// </summary>
/// <remarks>
///     A container is an item whose graphic has the tiledata Container flag; <see cref="IContainerLayoutService" />
///     gives its gump.
/// </remarks>
public sealed class UseRequestPacketHandler : IPacketHandler<UseRequestPacket>
{
    private readonly ILogger _logger = Log.ForContext<UseRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly ITileDataService _tiles;
    private readonly IContainerLayoutService _layouts;
    private readonly IPacketSendService _sender;

    public UseRequestPacketHandler(
        IItemService items,
        ITileDataService tiles,
        IContainerLayoutService layouts,
        IPacketSendService sender
    )
    {
        _items = items;
        _tiles = tiles;
        _layouts = layouts;
        _sender = sender;
    }

    public void Handle(GameSession session, UseRequestPacket packet)
    {
        if (!session.CharacterId.IsValid || !_items.TryGet(packet.Target, out var item))
        {
            _logger.Debug("Session {SessionId} used {Target}, which is not a live item", session.SessionId, packet.Target);

            return;
        }

        if (!_tiles.TryGetItem(item.ItemId, out var tile) || (tile.Flags & TileFlagType.Container) == 0)
        {
            _logger.Debug("Session {SessionId} used {Item}, which is not a container", session.SessionId, item);

            return;
        }

        if (_items.GetOwner(item) != session.CharacterId)
        {
            _logger.Debug("Session {SessionId} tried to open {Item}, which its character does not carry", session.SessionId, item);

            return;
        }

        var gump = _layouts.GetLayout(item.ItemId).Gump;
        _sender.TrySend(session.SessionId, new DisplayContainerPacket(item.Id, gump, session.UsesHighSeasContainers()));
        _sender.TrySend(session.SessionId, new ContainerContentPacket(_items.GetContents(item.Id), session.UsesContainerGrid()));
    }
}
