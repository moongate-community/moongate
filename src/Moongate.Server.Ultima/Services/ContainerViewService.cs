using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Sends the container gump (0x24), its items (0x3C) and the tooltip revision of each item, as ModernUO and Source-X.
/// </summary>
public sealed class ContainerViewService : IContainerViewService
{
    private readonly IItemService _items;
    private readonly IContainerLayoutService _layouts;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;

    public ContainerViewService(
        IItemService items,
        IContainerLayoutService layouts,
        IPacketSendService sender,
        ITooltipService tooltips
    )
    {
        _items = items;
        _layouts = layouts;
        _sender = sender;
        _tooltips = tooltips;
    }

    public void Show(GameSession session, ItemEntity container)
    {
        var gump = _layouts.GetLayout(container.ItemId).Gump;
        _sender.TrySend(session.SessionId, new DisplayContainerPacket(container.Id, gump, session.UsesHighSeasContainers()));
        var contents = _items.GetContents(container.Id);
        _sender.TrySend(session.SessionId, new ContainerContentPacket(contents, session.UsesContainerGrid()));

        foreach (var content in contents)
        {
            _sender.TrySend(session.SessionId, _tooltips.Info(content));
        }
    }
}
