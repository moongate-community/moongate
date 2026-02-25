using Moongate.Server.Http.Data;

namespace Moongate.Server.Http.Internal;

/// <summary>
/// Runtime dependencies used by mapped HTTP routes.
/// </summary>
internal sealed class MoongateHttpRouteContext
{
    public MoongateHttpRouteContext(
        Func<MoongateHttpMetricsSnapshot?>? metricsSnapshotFactory,
        MoongateHttpJwtOptions jwtOptions,
        Func<string, string, CancellationToken, Task<MoongateHttpAuthenticatedUser?>>? authenticateUserAsync,
        Func<MoongateHttpMetricsSnapshot, string> prometheusPayloadBuilder
    )
    {
        MetricsSnapshotFactory = metricsSnapshotFactory;
        JwtOptions = jwtOptions;
        AuthenticateUserAsync = authenticateUserAsync;
        PrometheusPayloadBuilder = prometheusPayloadBuilder;
    }

    public Func<string, string, CancellationToken, Task<MoongateHttpAuthenticatedUser?>>? AuthenticateUserAsync
    {
        get;
    }

    public MoongateHttpJwtOptions JwtOptions { get; }

    public Func<MoongateHttpMetricsSnapshot?>? MetricsSnapshotFactory { get; }

    public Func<MoongateHttpMetricsSnapshot, string> PrometheusPayloadBuilder { get; }
}
