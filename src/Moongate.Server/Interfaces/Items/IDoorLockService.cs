using Moongate.Server.Data.Items;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Items;

/// <summary>
/// Applies and removes lock metadata for world doors and linked door pairs.
/// </summary>
public interface IDoorLockService
{
    /// <summary>
    /// Locks the specified door, creating or reusing a shared lock identifier.
    /// </summary>
    /// <param name="doorId">Door serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result describing whether the door was locked and the resolved lock id.</returns>
    Task<DoorLockResult> LockDoorAsync(Serial doorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes lock metadata from the specified door and its linked counterpart.
    /// </summary>
    /// <param name="doorId">Door serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when unlocked; otherwise <c>false</c>.</returns>
    Task<bool> UnlockDoorAsync(Serial doorId, CancellationToken cancellationToken = default);
}
