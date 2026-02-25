using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;

namespace Moongate.Server.Http.Interfaces.Facades;

/// <summary>
/// Provides authentication operations for HTTP endpoints.
/// </summary>
public interface IHttpAuthFacade
{
    /// <summary>
    /// Authenticates an HTTP user.
    /// </summary>
    Task<MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    );
}
