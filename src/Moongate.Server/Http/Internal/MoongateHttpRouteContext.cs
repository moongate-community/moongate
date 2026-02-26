using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Metrics;

namespace Moongate.Server.Http.Internal;

/// <summary>
/// Runtime dependencies used by mapped HTTP routes.
/// </summary>
internal sealed class MoongateHttpRouteContext
{
    public MoongateHttpRouteContext(
        MoongateHttpJwtOptions jwtOptions,
        IAccountService? accountService,
        IMetricsHttpSnapshotFactory? metricsHttpSnapshotFactory,
        bool isUiEnabled
    )
    {
        JwtOptions = jwtOptions;
        AccountService = accountService;
        MetricsHttpSnapshotFactory = metricsHttpSnapshotFactory;
        IsUiEnabled = isUiEnabled;
    }

    public IAccountService? AccountService { get; }

    public MoongateHttpJwtOptions JwtOptions { get; }

    public IMetricsHttpSnapshotFactory? MetricsHttpSnapshotFactory { get; }

    public bool IsUiEnabled { get; }
}
