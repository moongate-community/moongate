using Moongate.Server.Http.Data.Results;

namespace Moongate.Server.Http.Interfaces.Facades;

/// <summary>
/// Provides system endpoints behavior for root, health and metrics.
/// </summary>
public interface IHttpSystemFacade
{
    /// <summary>
    /// Gets health status payload.
    /// </summary>
    Task<MoongateHttpOperationResult<string>> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics payload.
    /// </summary>
    Task<MoongateHttpOperationResult<string>> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets root endpoint payload.
    /// </summary>
    Task<MoongateHttpOperationResult<string>> GetRootAsync(CancellationToken cancellationToken = default);
}
