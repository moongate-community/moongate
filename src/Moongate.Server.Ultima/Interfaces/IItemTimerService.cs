using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The timers an item keeps: each has a name, is saved with the item as a prop and, when its time comes, runs
///     <c>on_timer(serial, name)</c> of the item's script, also after a restart. Called on the game loop.
/// </summary>
public interface IItemTimerService
{
    /// <summary>
    ///     Starts the timer <paramref name="name" /> of the item, or starts it again from now; false for a blank or too
    ///     long name or a delay that is not positive or is over a year.
    /// </summary>
    bool Start(ItemEntity item, string name, TimeSpan delay);

    /// <summary>
    ///     Stops the timer; false when the item has no timer of that name.
    /// </summary>
    bool Stop(ItemEntity item, string name);

    /// <summary>
    ///     Gets how long the timer still has to run, zero when it is due; null when the item has no timer of that name.
    /// </summary>
    TimeSpan? Remaining(ItemEntity item, string name);
}
