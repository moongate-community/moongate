using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;
using ZLinq;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Thread-safe account repository backed by the shared persistence state store.
/// </summary>
internal sealed class AccountRepository : BaseRepository<UOAccountEntity, Serial>, IAccountRepository
{
    private readonly ILogger _logger = Log.ForContext<AccountRepository>();

    internal AccountRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<UOAccountEntity, Serial> descriptor
    )
        : base(stateStore, journalService, descriptor) { }

    public async ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Account add requested for Id={AccountId} Username={Username}", account.Id, account.Username);
        var normalizedUsername = account.Username.Trim();
        bool inserted;
        JournalEntry? entry = null;

        lock (_stateStore.SyncRoot)
        {
            if (_stateStore.AccountsById.ContainsKey(account.Id) ||
                _stateStore.AccountNameIndex.ContainsKey(normalizedUsername))
            {
                inserted = false;
            }
            else
            {
                var clone = PrepareEntityForStore(account);
                _stateStore.AccountsById[clone.Id] = clone;
                BeforeUpsertLocked(clone, null);
                inserted = true;
                entry = CreateUpsertEntry(clone);
            }
        }

        if (inserted && entry is not null)
        {
            await AppendAsync(entry, cancellationToken);
        }

        _logger.Verbose(
            "Account add completed for Id={AccountId} Username={Username} Inserted={Inserted}",
            account.Id,
            normalizedUsername,
            inserted
        );

        return inserted;
    }

    public ValueTask<bool> ExistsAsync(
        Func<UOAccountEntity, bool> predicate,
        CancellationToken cancellationToken = default
    )
    {
        _logger.Verbose("Account exists query requested");
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_stateStore.SyncRoot)
        {
            var exists = _stateStore.AccountsById.Values.AsValueEnumerable().Any(predicate);
            _logger.Verbose("Account exists query completed Exists={Exists}", exists);

            return ValueTask.FromResult(exists);
        }
    }

    public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Account get-by-username requested for Username={Username}", username);
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            if (!_stateStore.AccountNameIndex.TryGetValue(username.Trim(), out var serial))
            {
                return ValueTask.FromResult<UOAccountEntity?>(null);
            }

            return ValueTask.FromResult(
                _stateStore.AccountsById.TryGetValue(serial, out var account) ? CloneEntity(account) : null
            );
        }
    }

    public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
        Func<UOAccountEntity, bool> predicate,
        Func<UOAccountEntity, TResult> selector,
        CancellationToken cancellationToken = default
    )
    {
        _logger.Verbose("Account query requested");
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(selector);

        UOAccountEntity[] snapshot;

        lock (_stateStore.SyncRoot)
        {
            snapshot = [.. _stateStore.AccountsById.Values.Select(CloneEntity)];
        }

        var results = snapshot.AsValueEnumerable().Where(predicate).Select(selector).ToArray();
        _logger.Verbose("Account query completed with Count={Count}", results.Length);

        return ValueTask.FromResult<IReadOnlyList<TResult>>(results);
    }

    protected override void AfterRemoveLocked(Serial key, UOAccountEntity entity)
        => _stateStore.AccountNameIndex.Remove(entity.Username);

    protected override void BeforeUpsertLocked(UOAccountEntity entity, UOAccountEntity? existing)
    {
        if (existing is not null && !existing.Username.Equals(entity.Username, StringComparison.OrdinalIgnoreCase))
        {
            _stateStore.AccountNameIndex.Remove(existing.Username);
        }

        _stateStore.AccountNameIndex[entity.Username] = entity.Id;
        _stateStore.LastAccountId = Math.Max(_stateStore.LastAccountId, (uint)entity.Id);
    }

    protected override UOAccountEntity PrepareEntityForStore(UOAccountEntity entity)
    {
        var clone = base.PrepareEntityForStore(entity);
        clone.Username = clone.Username.Trim();

        return clone;
    }
}
