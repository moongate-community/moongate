using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The timers of the live items, ordered by when they are due. An item keeps each of its timers as the prop
///     <c>timer.&lt;name&gt;</c>, saved with it; the queue only says when to look. The item service tells it of every
///     item that enters the world, the timer service takes the due ones. Called on the game loop.
/// </summary>
public interface IItemTimerQueue
{
    /// <summary>
    ///     Queues the timer <paramref name="name" /> of an item, due at <paramref name="dueAt" /> milliseconds since
    ///     1970 (UTC).
    /// </summary>
    void Schedule(Serial item, string name, long dueAt);

    /// <summary>
    ///     The item entered the world, loaded at startup or with its character: the timers its props keep are queued.
    /// </summary>
    void Track(ItemEntity item);

    /// <summary>
    ///     Takes the entries that are due, the earliest first. An entry may be stale: its item gone, or its timer
    ///     stopped or started again since.
    /// </summary>
    IReadOnlyList<ItemTimerEntry> TakeDue();
}
