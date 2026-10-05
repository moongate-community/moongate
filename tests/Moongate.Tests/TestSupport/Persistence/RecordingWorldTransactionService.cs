using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Runs each world transaction against in-memory mobiles and items; a transaction that throws leaves them as they
///     were, as a rollback would. Counts the transactions and records the deleted serials.
/// </summary>
public sealed class RecordingWorldTransactionService : IWorldTransactionService
{
    public RecordingDataAccess<MobileEntity> Mobiles { get; } = new();

    public RecordingDataAccess<ItemEntity> Items { get; } = new();

    public List<Serial> Deleted { get; } = [];

    public int Transactions { get; private set; }

    public Task? Hold { get; set; }

    public async Task ExecuteAsync(Func<IPersistenceTransaction, Task> operation, CancellationToken cancellationToken = default)
    {
        if (Hold is not null)
        {
            await Hold;
        }

        Transactions++;
        var mobiles = Mobiles.Upserted.Count;
        var items = Items.Upserted.Count;
        var deleted = Deleted.Count;

        try
        {
            await operation(new Transaction(this));
        }
        catch
        {
            Mobiles.Upserted.RemoveRange(mobiles, Mobiles.Upserted.Count - mobiles);
            Items.Upserted.RemoveRange(items, Items.Upserted.Count - items);
            Deleted.RemoveRange(deleted, Deleted.Count - deleted);

            throw;
        }
    }

    private sealed class Transaction : IPersistenceTransaction
    {
        private readonly RecordingWorldTransactionService _owner;

        public Transaction(RecordingWorldTransactionService owner)
        {
            _owner = owner;
        }

        public Task InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IMoongateEntity
        {
            return GetDataAccess<T>().UpsertAsync(entity, cancellationToken);
        }

        public Task<T?> GetByIdForUpdateAsync<T>(Serial id, CancellationToken cancellationToken = default)
            where T : class, IMoongateEntity
        {
            return GetDataAccess<T>().GetByIdAsync(id, cancellationToken);
        }

        public IDataAccess<T> GetDataAccess<T>() where T : class, IMoongateEntity
        {
            if (typeof(T) == typeof(MobileEntity))
            {
                return (IDataAccess<T>)(object)_owner.Mobiles;
            }

            if (typeof(T) == typeof(ItemEntity))
            {
                return (IDataAccess<T>)(object)new DeletingItems(_owner);
            }

            throw new NotSupportedException(typeof(T).Name);
        }
    }

    private sealed class DeletingItems : IDataAccess<ItemEntity>
    {
        private readonly RecordingWorldTransactionService _owner;

        public DeletingItems(RecordingWorldTransactionService owner)
        {
            _owner = owner;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _owner.Deleted.Add(id);

            return Task.FromResult(true);
        }

        public Task<Serial> ReserveSerialAsync(CancellationToken cancellationToken = default)
        {
            return _owner.Items.ReserveSerialAsync(cancellationToken);
        }

        public Task<IReadOnlyList<ItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return _owner.Items.GetAllAsync(cancellationToken);
        }

        public Task<ItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            return _owner.Items.GetByIdAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<ItemEntity>> QueryAsync(
            System.Linq.Expressions.Expression<Func<ItemEntity, bool>> predicate,
            CancellationToken cancellationToken = default
        )
        {
            return _owner.Items.QueryAsync(predicate, cancellationToken);
        }

        public Task<IReadOnlyList<ItemEntity>> QueryAsync(
            System.Linq.Expressions.Expression<Func<ItemEntity, bool>> predicate,
            int skip,
            int take,
            CancellationToken cancellationToken = default
        )
        {
            return _owner.Items.QueryAsync(predicate, skip, take, cancellationToken);
        }

        public Task UpsertAsync(ItemEntity entity, CancellationToken cancellationToken = default)
        {
            return _owner.Items.UpsertAsync(entity, cancellationToken);
        }
    }
}
