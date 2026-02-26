using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Services.Metrics;

namespace Moongate.Tests.Server.Http.Support;

public sealed class TestMetricsHttpSnapshotFactory(Func<MoongateHttpMetricsSnapshot?> createSnapshot)
    : IMetricsHttpSnapshotFactory
{
    public MoongateHttpMetricsSnapshot? CreateSnapshot()
        => createSnapshot();
}
