using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

/// <summary>
///     Hands out the serials a test queued for deletion and records what the save reported as committed.
/// </summary>
public sealed class RecordingDeletionSource : IPersistenceDeletionSource
{
    public List<Serial> Pending { get; } = [];

    public List<IReadOnlyCollection<Serial>> Committed { get; } = [];

    public IReadOnlyCollection<Serial> Capture()
    {
        return Pending.ToArray();
    }

    void IPersistenceDeletionSource.Committed(IReadOnlyCollection<Serial> serials)
    {
        Committed.Add(serials);
    }
}
