using Moongate.Server.Core.Data.Diagnostics;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Runs diagnostic collection and exposes its latest immutable snapshot to concurrent readers.
/// </summary>
/// <remarks>
///     Lifecycle methods coordinate worker cancellation internally; snapshot reads may run concurrently with collection
///     and lifecycle operations.
/// </remarks>
public interface IDiagnosticService : IMoongateStartupService
{
    /// <summary>
    ///     Returns the latest published snapshot, or <see langword="null" /> before the first completed collection.
    /// </summary>
    DiagnosticSnapshot? GetSnapshot();
}
