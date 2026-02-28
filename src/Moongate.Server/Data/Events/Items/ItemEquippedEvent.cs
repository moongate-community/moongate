using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Events.Items;

/// <summary>
/// Event emitted when an item is equipped by a mobile on a specific layer.
/// </summary>
public readonly record struct ItemEquippedEvent(
    GameEventBase BaseEvent,
    Serial ItemId,
    Serial MobileId,
    ItemLayerType Layer
) : IGameEvent
{
    /// <summary>
    /// Creates an item-equipped event with current timestamp.
    /// </summary>
    public ItemEquippedEvent(
        Serial itemId,
        Serial mobileId,
        ItemLayerType layer
    )
        : this(GameEventBase.CreateNow(), itemId, mobileId, layer) { }
}
