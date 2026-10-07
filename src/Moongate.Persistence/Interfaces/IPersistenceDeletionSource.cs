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

    /// <summary>
    ///     Gets the identities this save must write whatever their fingerprint: entities that did not change but whose
    ///     row may be gone, such as the contents of a container taken out of something the save deletes, which the
    ///     database removes with it. Captured and reported as <see cref="Capture" /> and <see cref="Committed" /> are.
    ///     None by default.
    /// </summary>
    IReadOnlyCollection<Serial> CaptureRewrites()
    {
        return [];
    }

    /// <summary>
    ///     Reports that the entities of <paramref name="serials" /> were written and committed.
    /// </summary>
    void RewritesCommitted(IReadOnlyCollection<Serial> serials)
    {
    }
}
