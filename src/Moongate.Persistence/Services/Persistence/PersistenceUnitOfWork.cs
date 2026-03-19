using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Types;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Coordinates repositories plus snapshot/journal load-save lifecycle.
/// </summary>
public sealed class PersistenceUnitOfWork : IPersistenceUnitOfWork, IDisposable
{
    private readonly IPersistenceEntityRegistry _entityRegistry;
    private readonly Dictionary<(Type EntityType, Type KeyType), object> _genericRepositories = [];
    private readonly BinaryJournalService _journalService;
    private readonly ILogger _logger = Log.ForContext<PersistenceUnitOfWork>();
    private readonly MessagePackSnapshotService _snapshotService;
    private readonly PersistenceStateStore _stateStore = new();

    public PersistenceUnitOfWork(PersistenceOptions options, IPersistenceEntityRegistry? entityRegistry = null)
    {
        _entityRegistry = entityRegistry ?? new PersistenceEntityRegistry();
        PersistenceCoreDescriptors.EnsureRegistered(_entityRegistry);
        _entityRegistry.Freeze();

        _snapshotService = new(options.SnapshotFilePath, options.EnableFileLock);
        _journalService = new(options.JournalFilePath, options.EnableFileLock);
        var accountDescriptor = _entityRegistry.GetDescriptor<UOAccountEntity, Serial>();
        var mobileDescriptor = _entityRegistry.GetDescriptor<UOMobileEntity, Serial>();
        var itemDescriptor = _entityRegistry.GetDescriptor<UOItemEntity, Serial>();
        var bulletinBoardMessageDescriptor = _entityRegistry.GetDescriptor<BulletinBoardMessageEntity, Serial>();
        var helpTicketDescriptor = _entityRegistry.GetDescriptor<HelpTicketEntity, Serial>();

        Accounts = new AccountRepository(_stateStore, _journalService, accountDescriptor);
        Mobiles = new MobileRepository(_stateStore, _journalService, mobileDescriptor);
        Items = new ItemRepository(_stateStore, _journalService, itemDescriptor);
        BulletinBoardMessages = new BulletinBoardMessageRepository(
            _stateStore,
            _journalService,
            bulletinBoardMessageDescriptor
        );
        HelpTickets = new HelpTicketRepository(_stateStore, _journalService, helpTicketDescriptor);
    }

    public IAccountRepository Accounts { get; }

    public IItemRepository Items { get; }

    public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

    public IHelpTicketRepository HelpTickets { get; }

    public IMobileRepository Mobiles { get; }

    public IBaseRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
    {
        object repository = typeof(TEntity) switch
        {
            var entityType when entityType == typeof(UOAccountEntity) && typeof(TKey) == typeof(Serial) => Accounts,
            var entityType when entityType == typeof(UOMobileEntity) && typeof(TKey) == typeof(Serial) => Mobiles,
            var entityType when entityType == typeof(UOItemEntity) && typeof(TKey) == typeof(Serial) => Items,
            var entityType when entityType == typeof(BulletinBoardMessageEntity) && typeof(TKey) == typeof(Serial) =>
                BulletinBoardMessages,
            var entityType when entityType == typeof(HelpTicketEntity) && typeof(TKey) == typeof(Serial) => HelpTickets,
            _ => GetOrCreateGenericRepository<TEntity, TKey>()
        };

        return (IBaseRepository<TEntity, TKey>)repository;
    }

    public Serial AllocateNextAccountId()
    {
        lock (_stateStore.SyncRoot)
        {
            _stateStore.LastAccountId++;

            return (Serial)_stateStore.LastAccountId;
        }
    }

    public Serial AllocateNextItemId()
    {
        lock (_stateStore.SyncRoot)
        {
            _stateStore.LastItemId++;

            return (Serial)_stateStore.LastItemId;
        }
    }

    public Serial AllocateNextMobileId()
    {
        lock (_stateStore.SyncRoot)
        {
            _stateStore.LastMobileId++;

            return (Serial)_stateStore.LastMobileId;
        }
    }

    public ValueTask<CapturedWorldSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Persistence snapshot-capture requested");
        cancellationToken.ThrowIfCancellationRequested();
        WorldSnapshot snapshot;
        long capturedLastSequenceId;

        lock (_stateStore.SyncRoot)
        {
            snapshot = new()
            {
                Version = 1,
                CreatedUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastSequenceId = _stateStore.LastSequenceId,
                EntityBuckets =
                [
                    .. _entityRegistry.GetRegisteredDescriptors()
                                     .Select(
                                         descriptor =>
                                             ((IInternalPersistenceEntityDescriptor)descriptor).CaptureBucket(_stateStore)
                                     )
                                     .OfType<EntitySnapshotBucket>()
                ],
            };
            capturedLastSequenceId = _stateStore.LastSequenceId;
        }

        return ValueTask.FromResult(
            new CapturedWorldSnapshot
            {
                Snapshot = snapshot,
                CapturedLastSequenceId = capturedLastSequenceId
            }
        );
    }

    public void Dispose()
    {
        _journalService.Dispose();
        _snapshotService.Dispose();
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Persistence initialize requested");
        var snapshot = await _snapshotService.LoadAsync(cancellationToken);

        lock (_stateStore.SyncRoot)
        {
            _stateStore.ClearBuckets();
            _stateStore.AccountNameIndex.Clear();
            _stateStore.LastSequenceId = 0;
            _stateStore.LastAccountId = Serial.MobileStart - 1;
            _stateStore.LastMobileId = Serial.MobileStart - 1;
            _stateStore.LastItemId = Serial.ItemOffset - 1;

            if (snapshot is not null)
            {
                foreach (var bucket in snapshot.EntityBuckets)
                {
                    if (!_entityRegistry.IsRegistered(bucket.TypeId))
                    {
                        throw new InvalidOperationException(
                            $"No persistence descriptor registered for snapshot bucket type id {bucket.TypeId}."
                        );
                    }

                    ((IInternalPersistenceEntityDescriptor)_entityRegistry.GetDescriptor(bucket.TypeId))
                        .LoadBucket(_stateStore, bucket);
                }

                _stateStore.LastSequenceId = snapshot.LastSequenceId;
                RebuildAccountNameIndex();
            }
        }

        var journalEntries = await _journalService.ReadAllAsync(cancellationToken);

        lock (_stateStore.SyncRoot)
        {
            foreach (var entry in journalEntries.OrderBy(e => e.SequenceId))
            {
                ApplyEntry(entry);

                if (entry.SequenceId > _stateStore.LastSequenceId)
                {
                    _stateStore.LastSequenceId = entry.SequenceId;
                }
            }

            RecalculateLastEntityIds();
            RebuildAccountNameIndex();
        }

        _logger.Information(
            "Persistence initialize completed Accounts={AccountCount} Mobiles={MobileCount} Items={ItemCount} LastSequenceId={LastSequenceId}",
            _stateStore.AccountsById.Count,
            _stateStore.MobilesById.Count,
            _stateStore.ItemsById.Count,
            _stateStore.LastSequenceId
        );
    }

    public async ValueTask SaveCapturedSnapshotAsync(
        CapturedWorldSnapshot capturedSnapshot,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(capturedSnapshot);
        _logger.Verbose(
            "Persistence captured-snapshot save requested LastSequenceId={LastSequenceId}",
            capturedSnapshot.CapturedLastSequenceId
        );
        await _snapshotService.SaveAsync(capturedSnapshot.Snapshot, cancellationToken);
        await _journalService.TrimThroughSequenceAsync(capturedSnapshot.CapturedLastSequenceId, cancellationToken);
        _logger.Verbose(
            "Persistence captured-snapshot save completed LastSequenceId={LastSequenceId}",
            capturedSnapshot.CapturedLastSequenceId
        );
    }

    public async ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Persistence snapshot-save requested");
        var captured = await CaptureSnapshotAsync(cancellationToken);
        await SaveCapturedSnapshotAsync(captured, cancellationToken);
    }

    private void ApplyEntry(JournalEntry entry)
    {
        if (!_entityRegistry.IsRegistered(entry.TypeId))
        {
            throw new InvalidOperationException($"No persistence descriptor registered for journal type id {entry.TypeId}.");
        }

        var descriptor = (IInternalPersistenceEntityDescriptor)_entityRegistry.GetDescriptor(entry.TypeId);

        switch (entry.Operation)
        {
            case JournalEntityOperationType.Upsert:
                descriptor.ApplyUpsert(_stateStore, entry.Payload);
                break;
            case JournalEntityOperationType.Remove:
                descriptor.ApplyRemove(_stateStore, entry.Payload);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported journal entity operation '{entry.Operation}' for type id {entry.TypeId}."
                );
        }
    }

    private IBaseRepository<TEntity, TKey> GetOrCreateGenericRepository<TEntity, TKey>()
    {
        var cacheKey = (typeof(TEntity), typeof(TKey));

        lock (_genericRepositories)
        {
            if (_genericRepositories.TryGetValue(cacheKey, out var existing))
            {
                return (IBaseRepository<TEntity, TKey>)existing;
            }

            var descriptor = _entityRegistry.GetDescriptor<TEntity, TKey>();
            var repository = new GenericRepository<TEntity, TKey>(_stateStore, _journalService, descriptor);
            _genericRepositories[cacheKey] = repository;

            return (IBaseRepository<TEntity, TKey>)repository;
        }
    }

    private void RecalculateLastEntityIds()
    {
        _stateStore.LastAccountId = _stateStore.AccountsById.Count == 0
                                        ? Serial.MobileStart - 1
                                        : _stateStore.AccountsById.Keys.Max(static id => (uint)id);

        _stateStore.LastMobileId = _stateStore.MobilesById.Count == 0
                                       ? Serial.MobileStart - 1
                                       : _stateStore.MobilesById.Keys.Max(static id => (uint)id);

        _stateStore.LastItemId = _stateStore.ItemsById.Count == 0
                                     ? Serial.ItemOffset - 1
                                     : _stateStore.ItemsById.Keys.Max(static id => (uint)id);

        if (_stateStore.BulletinBoardMessagesById.Count > 0)
        {
            _stateStore.LastItemId = Math.Max(
                _stateStore.LastItemId,
                _stateStore.BulletinBoardMessagesById.Keys.Max(static id => (uint)id)
            );
        }

        if (_stateStore.HelpTicketsById.Count > 0)
        {
            _stateStore.LastItemId = Math.Max(
                _stateStore.LastItemId,
                _stateStore.HelpTicketsById.Keys.Max(static id => (uint)id)
            );
        }
    }

    private void RebuildAccountNameIndex()
    {
        _stateStore.AccountNameIndex.Clear();

        foreach (var account in _stateStore.AccountsById.Values)
        {
            _stateStore.AccountNameIndex[account.Username] = account.Id;
        }
    }
}
