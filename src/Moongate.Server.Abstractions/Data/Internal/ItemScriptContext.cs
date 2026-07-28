using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Ultima.Types;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// What an item-script hook is told: the item it fired for, who caused it, and the two fields that
/// only some hooks carry. <see cref="Actor" /> is null when nobody caused it — a script fired by the
/// world rather than by a player.
/// </summary>
public sealed record ItemScriptContext(
    ItemEntity Item,
    MobileEntity? Actor,
    Serial ContainerId,
    LayerType? Layer
)
{
    /// <summary>The shape for the three hooks that carry neither a container nor a layer.</summary>
    public static ItemScriptContext For(ItemEntity item, MobileEntity? actor)
        => new(item, actor, Serial.Zero, null);
}
