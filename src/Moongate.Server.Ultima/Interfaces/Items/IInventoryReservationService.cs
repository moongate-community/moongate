using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Interfaces.Items;

/// <summary>
///     Excludes owner inventory changes until a supervised delivery settles on the loop.
/// </summary>
public interface IInventoryReservationService
{
    /// <summary>
    ///     Reserves an owner once, with the task logout must await.
    /// </summary>
    bool TryReserve(Serial mobileId, Task settlement);

    /// <summary>
    ///     Returns whether ordinary changes to this owner are excluded.
    /// </summary>
    bool IsReserved(Serial mobileId);

    /// <summary>
    ///     Gets the current settlement, or a completed task.
    /// </summary>
    Task WaitAsync(Serial mobileId);

    /// <summary>
    ///     Releases a safely settled reservation.
    /// </summary>
    void Release(Serial mobileId);

    /// <summary>
    ///     Runs synchronous supervised application with a scoped owner permit.
    /// </summary>
    void Apply(Serial mobileId, Action application);
}
