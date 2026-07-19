using Moongate.Network.Data;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Containers;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Opens a container (0x24) and fills it (0x3C) when a player double-clicks one. Whether an item is a
/// container is the template's answer, reached through <see cref="ItemEntity.TemplateId" />: ModernUO
/// asks the same question of its class hierarchy, which an entity built from data does not have.
/// </summary>
public sealed class ContainerSubscriber : IEventSubscriberRegistration
{
    private readonly ISessionManager _sessions;
    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly IContainerGumpService _gumps;
    private readonly IOplService _opl;

    public ContainerSubscriber(
        ISessionManager sessions,
        IItemService items,
        IItemTemplateService templates,
        IContainerGumpService gumps,
        IOplService opl
    )
    {
        _sessions = sessions;
        _items = items;
        _templates = templates;
        _gumps = gumps;
        _opl = opl;
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
                new(
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

    /// <summary>
    /// The gump to open the item with, or null when it is not a container. The template's own
    /// <c>GumpId</c> wins; failing that the gump table is asked for one matching the graphic; failing
    /// that it is the plain bag. This is ModernUO's chain — an overridden <c>DefaultGumpID</c>, then
    /// <c>ContainerData.GetData(itemID)</c>, then that table's default entry — and it is why the
    /// backpack is listed in neither: it lands on the default.
    /// </summary>
    public int? ResolveGumpId(ItemEntity item)
    {
        if (_templates.GetById(item.TemplateId)?.Container is not { } container)
        {
            return null;
        }

        return container.GumpId ?? _gumps.GetByItemId(item.ItemId)?.GumpId ?? ContainerGumpLayout.DefaultGumpId;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<ItemDoubleClickEvent>(OnDoubleClick);

    private Task OnDoubleClick(ItemDoubleClickEvent message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGet(message.SessionId, out var session) || _items.GetById(message.Serial) is not { } item)
        {
            return Task.CompletedTask;
        }

        if (ResolveGumpId(item) is not { } gumpId)
        {
            return Task.CompletedTask;
        }

        var contents = _items.GetContents(item.Id);

        session.Send(new DrawContainerPacket(item.Id, (ushort)gumpId));
        session.Send(new ContainerContentPacket(item.Id, BuildContents(contents)));

        // Prime the client's tooltip cache for what it can now see.
        foreach (var contained in contents)
        {
            var snapshot = _opl.GetOrBuild(contained.Id);

            if (snapshot.HasEntries)
            {
                session.Send(new OplInfoPacket(contained.Id, snapshot.Hash));
            }
        }

        return Task.CompletedTask;
    }
}
