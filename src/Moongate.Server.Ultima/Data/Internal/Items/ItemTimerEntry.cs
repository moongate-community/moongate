using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Internal.Items;

/// <summary>
///     A timer of an item waiting in the queue: the item, the timer's name and when it is due, in milliseconds since
///     1970 (UTC), as the item's prop keeps it.
/// </summary>
public sealed record ItemTimerEntry(Serial Item, string Name, long DueAt);
