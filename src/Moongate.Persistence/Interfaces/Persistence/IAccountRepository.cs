using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Provides persistence operations for account entities.
/// </summary>
public interface IAccountRepository : IBaseRepository<UOAccountEntity, Serial>
{
    /// <summary>
    /// Adds a new account if the identifier and username are not already present.
    /// </summary>
    ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an account by username.
    /// </summary>
    ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when at least one account matches the predicate.
    /// </summary>
    ValueTask<bool> ExistsAsync(
        Func<UOAccountEntity, bool> predicate,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Runs a projection query over account entities.
    /// </summary>
    ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
        Func<UOAccountEntity, bool> predicate,
        Func<UOAccountEntity, TResult> selector,
        CancellationToken cancellationToken = default
    );
}
