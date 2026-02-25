using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Server.Http.Interfaces.Facades;

namespace Moongate.Tests.Server.Http.Support;

public sealed class TestHttpAuthFacade(
    Func<string, string, CancellationToken, Task<MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>>> authenticateAsync
) : IHttpAuthFacade
{
    public Task<MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
        => authenticateAsync(username, password, cancellationToken);
}

public sealed class TestHttpUsersFacade(
    Func<CancellationToken, Task<MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>>> getUsersAsync,
    Func<string, CancellationToken, Task<MoongateHttpOperationResult<MoongateHttpUser>>> getUserByIdAsync,
    Func<MoongateHttpCreateUserRequest, CancellationToken, Task<MoongateHttpOperationResult<MoongateHttpUser>>> createUserAsync,
    Func<string, MoongateHttpUpdateUserRequest, CancellationToken, Task<MoongateHttpOperationResult<MoongateHttpUser>>>
        updateUserAsync,
    Func<string, CancellationToken, Task<MoongateHttpOperationResult<object?>>> deleteUserAsync
) : IHttpUsersFacade
{
    public Task<MoongateHttpOperationResult<MoongateHttpUser>> CreateUserAsync(
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken = default
    )
        => createUserAsync(request, cancellationToken);

    public Task<MoongateHttpOperationResult<object?>> DeleteUserAsync(
        string accountId,
        CancellationToken cancellationToken = default
    )
        => deleteUserAsync(accountId, cancellationToken);

    public Task<MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>> GetUsersAsync(
        CancellationToken cancellationToken = default
    )
        => getUsersAsync(cancellationToken);

    public Task<MoongateHttpOperationResult<MoongateHttpUser>> GetUserByIdAsync(
        string accountId,
        CancellationToken cancellationToken = default
    )
        => getUserByIdAsync(accountId, cancellationToken);

    public Task<MoongateHttpOperationResult<MoongateHttpUser>> UpdateUserAsync(
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken = default
    )
        => updateUserAsync(accountId, request, cancellationToken);
}
