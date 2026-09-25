using Moongate.Server.Data.Internal.Diagnostics;

namespace Moongate.Server.Interfaces.Diagnostics.Internal;

/// <summary>
///     Reads one process/GC snapshot; callers serialize reads and own disposal of the reader.
/// </summary>
internal interface IProcessMetricsReader : IDisposable
{
    /// <summary>
    ///     Synchronously reads current process values; the owner must serialize calls and disposal.
    /// </summary>
    ProcessMetricsReading Read();
}
