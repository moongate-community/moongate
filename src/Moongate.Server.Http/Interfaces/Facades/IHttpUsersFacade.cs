using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;

namespace Moongate.Server.Http.Interfaces.Facades;

/// <summary>
/// Provides user CRUD operations for HTTP endpoints.
/// </summary>
public interface IHttpUsersFacade
{
    /// <summary>
    /// Creates a user.
    /// </summary>
    Task<MoongateHttpOperationResult<MoongateHttpUser>> CreateUserAsync(
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes a user by account identifier.
    /// </summary>
    Task<MoongateHttpOperationResult<object?>> DeleteUserAsync(
        string accountId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns all users.
    /// </summary>
    Task<MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>> GetUsersAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns a user by account identifier.
    /// </summary>
    Task<MoongateHttpOperationResult<MoongateHttpUser>> GetUserByIdAsync(
        string accountId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates a user.
    /// </summary>
    Task<MoongateHttpOperationResult<MoongateHttpUser>> UpdateUserAsync(
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken = default
    );
}
