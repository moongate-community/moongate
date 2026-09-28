using Moongate.Core.Primitives;

namespace Moongate.Persistence.Interfaces;

/// <summary>
///     The identities a live source removed since the last save: the world save deletes them in the same transaction
///     as its upserts, so a removed entity cannot come back from a snapshot written later.
/// </summary>
/// <remarks>
///     <see cref="Capture" /> runs on the owner loop with the source capture. <see cref="Committed" /> runs after that
///     target's transaction commits, off the loop, with exactly what was captured; after a failed save it is not
///     called and the identities stay pending. Deleting an identity that no longer exists is harmless.
/// </remarks>
public interface IPersistenceDeletionSource
{
    /// <summary>
    ///     Gets the identities to delete in this save.
    /// </summary>
    IReadOnlyCollection<Serial> Capture();

    /// <summary>
    ///     Reports that <paramref name="serials" /> were deleted and committed.
    /// </summary>
    void Committed(IReadOnlyCollection<Serial> serials);
}
