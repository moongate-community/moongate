using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Server.Http.Interfaces.Facades;

namespace Moongate.Server.Http.Internal.Facades;

/// <summary>
/// Default system facade backed by metrics snapshot factory and payload builder.
/// </summary>
internal sealed class DefaultHttpSystemFacade : IHttpSystemFacade
{
    private readonly Func<MoongateHttpMetricsSnapshot?>? _metricsSnapshotFactory;
    private readonly Func<MoongateHttpMetricsSnapshot, string> _prometheusPayloadBuilder;

    public DefaultHttpSystemFacade(
        Func<MoongateHttpMetricsSnapshot?>? metricsSnapshotFactory,
        Func<MoongateHttpMetricsSnapshot, string> prometheusPayloadBuilder
    )
    {
        _metricsSnapshotFactory = metricsSnapshotFactory;
        _prometheusPayloadBuilder = prometheusPayloadBuilder;
    }

    public Task<MoongateHttpOperationResult<string>> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(MoongateHttpOperationResult<string>.Ok("ok"));

    public Task<MoongateHttpOperationResult<string>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (_metricsSnapshotFactory is null)
        {
            return Task.FromResult(MoongateHttpOperationResult<string>.NotFound("metrics endpoint is not configured"));
        }

        var snapshot = _metricsSnapshotFactory();
        if (snapshot is null)
        {
            return Task.FromResult(
                MoongateHttpOperationResult<string>.ServiceUnavailable("metrics are currently unavailable")
            );
        }

        var payload = _prometheusPayloadBuilder(snapshot);

        return Task.FromResult(MoongateHttpOperationResult<string>.Ok(payload));
    }

    public Task<MoongateHttpOperationResult<string>> GetRootAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(MoongateHttpOperationResult<string>.Ok("Moongate HTTP Service is running."));
}
