using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Events;
using Moongate.Server.Interfaces.Items;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Opens a container (0x24) and fills it (0x3C) when a player double-clicks one. An item counts as a
/// container when it has a gump: the entity does not remember its template, so that is the only trace
/// left of the template's container spec.
/// </summary>
public sealed class ContainerSubscriber : IEventSubscriberRegistration
{
    private readonly ISessionManager _sessions;
    private readonly IItemService _items;

    public ContainerSubscriber(ISessionManager sessions, IItemService items)
    {
        _sessions = sessions;
        _items = items;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<ItemDoubleClickEvent>(OnDoubleClick);

    private Task OnDoubleClick(ItemDoubleClickEvent message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGet(message.SessionId, out var session) || _items.GetById(message.Serial) is not { } item)
        {
            return Task.CompletedTask;
        }

        if (item.GumpId is not { } gumpId)
        {
            return Task.CompletedTask;
        }

        session.Send(new DrawContainerPacket(item.Id, (ushort)gumpId));
        session.Send(new ContainerContentPacket(item.Id, BuildContents(_items.GetContents(item.Id))));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Turns a container's direct children into wire entries. Nesting is not walked: a container inside
    /// this one is drawn as an item, and only opens when the player double-clicks it in turn.
    /// </summary>
    public static List<ContainerItem> BuildContents(IEnumerable<ItemEntity> items)
    {
        var contents = new List<ContainerItem>();

        foreach (var item in items)
        {
            contents.Add(
                new ContainerItem(
                    item.Id,
                    (ushort)item.ItemId,
                    (ushort)Math.Clamp(item.Amount, 1, ushort.MaxValue),
                    item.ContainerPosition,
                    item.Hue
                )
            );
        }

        return contents;
    }
}
