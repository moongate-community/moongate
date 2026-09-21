using Moongate.Server.Data.Internal.Diagnostics;
using Moongate.Server.Interfaces.Diagnostics.Internal;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class StubProcessMetricsReader : IProcessMetricsReader
{
    public ProcessMetricsReading Reading { get; set; }
    public int ReadCount { get; private set; }
    public bool IsDisposed { get; private set; }

    public StubProcessMetricsReader(ProcessMetricsReading reading)
    {
        Reading = reading;
    }

    public void Dispose()
        => IsDisposed = true;

    public ProcessMetricsReading Read()
    {
        ReadCount++;

        return Reading;
    }
}
