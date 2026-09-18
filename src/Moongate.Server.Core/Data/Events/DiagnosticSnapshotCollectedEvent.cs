using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Server.Core.Data.Events;

public sealed record DiagnosticSnapshotCollectedEvent : IMoongateEvent
{
    public DiagnosticSnapshot Snapshot { get; }

    public DiagnosticSnapshotCollectedEvent(DiagnosticSnapshot snapshot)
    {
        Snapshot = snapshot;
    }
}
