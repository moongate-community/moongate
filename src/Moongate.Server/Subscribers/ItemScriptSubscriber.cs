using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Types.Items;
using Moongate.Ultima.Types;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Turns the five item events into item-script hook calls. Every event it listens to is loop-affine,
/// so the hooks run on the game loop and a script may touch world state directly. Handlers are public
/// so tests can drive them without a dispatching event bus.
/// </summary>
public sealed class ItemScriptSubscriber : IEventSubscriberRegistration
{
    private readonly IItemService _items;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IItemScriptRuntime _scripts;
    private readonly ISessionManager _sessions;

    public ItemScriptSubscriber(
        IItemService items,
        IPersistenceService persistenceService,
        IItemScriptRuntime scripts,
        ISessionManager sessions
    )
    {
        _items = items;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _scripts = scripts;
        _sessions = sessions;
    }

    public Task OnItemDoubleClick(ItemDoubleClickEvent message, CancellationToken cancellationToken)
        => Dispatch(ItemScriptHookType.DoubleClick, message.Serial, Clicker(message.SessionId), Serial.Zero, null);

    public Task OnItemDropped(ItemDroppedEvent message, CancellationToken cancellationToken)
        => Dispatch(
            ItemScriptHookType.Dropped,
            message.Item,
            _mobiles.GetById(message.Actor),
            message.ContainerId,
            null
        );

    public Task OnItemEquipped(ItemEquippedEvent message, CancellationToken cancellationToken)
        => Dispatch(
            ItemScriptHookType.Equipped,
            message.Item,
            _mobiles.GetById(message.Mobile),
            Serial.Zero,
            message.Layer
        );

    public Task OnItemSingleClick(ItemSingleClickEvent message, CancellationToken cancellationToken)
        => Dispatch(ItemScriptHookType.SingleClick, message.Serial, Clicker(message.SessionId), Serial.Zero, null);

    public Task OnItemUnequipped(ItemUnequippedEvent message, CancellationToken cancellationToken)
        => Dispatch(
            ItemScriptHookType.Unequipped,
            message.Item,
            _mobiles.GetById(message.Mobile),
            Serial.Zero,
            message.Layer
        );

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<ItemSingleClickEvent>(OnItemSingleClick);
        eventBus.Subscribe<ItemDoubleClickEvent>(OnItemDoubleClick);
        eventBus.Subscribe<ItemDroppedEvent>(OnItemDropped);
        eventBus.Subscribe<ItemEquippedEvent>(OnItemEquipped);
        eventBus.Subscribe<ItemUnequippedEvent>(OnItemUnequipped);
    }

    /// <summary>The character behind a click, or null when the session is gone or has none.</summary>
    private MobileEntity? Clicker(long sessionId)
        => _sessions.TryGet(sessionId, out var session) ? session.Character : null;

    private Task Dispatch(
        ItemScriptHookType hook,
        Serial itemId,
        MobileEntity? actor,
        Serial containerId,
        LayerType? layer
    )
    {
        // A deleted item, or one whose script does not exist, is not an error: most items have none.
        if (_items.GetById(itemId) is { } item)
        {
            _scripts.Invoke(hook, new(item, actor, containerId, layer));
        }

        return Task.CompletedTask;
    }
}
