using System.Diagnostics;
using Moongate.Server.Data.Internal.Diagnostics;
using Moongate.Server.Interfaces.Diagnostics.Internal;

namespace Moongate.Server.Services.Diagnostics.Internal;

internal sealed class ProcessMetricsReader : IProcessMetricsReader
{
    private readonly Process _process;
    private bool _disposed;

    internal ProcessMetricsReader(Process process)
    {
        _process = process;
    }

    public ProcessMetricsReading Read()
    {
        _process.Refresh();
        return new ProcessMetricsReading
        {
            ProcessId = _process.Id,
            ProcessorCount = Environment.ProcessorCount,
            StartedAtUtc = new DateTimeOffset(_process.StartTime.ToUniversalTime()),
            TotalProcessorTime = _process.TotalProcessorTime,
            WorkingSetBytes = _process.WorkingSet64,
            PrivateMemoryBytes = _process.PrivateMemorySize64,
            ManagedMemoryBytes = GC.GetTotalMemory(false),
            ThreadCount = _process.Threads.Count,
            GcGen0Collections = GC.CollectionCount(0),
            GcGen1Collections = GC.CollectionCount(1),
            GcGen2Collections = GC.CollectionCount(2)
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.Dispose();
    }
}
