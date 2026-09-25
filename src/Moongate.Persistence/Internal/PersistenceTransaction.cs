using System.Data.Common;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceTransaction : IPersistenceTransaction
{
    private readonly Lock _sync = new();
    private readonly MoongatePersistenceService _owner;
    private readonly PostgreSqlDatabase _database;
    private readonly NpgsqlTransaction _transaction;
    private readonly CancellationToken _cancellationToken;
    private bool _accepting = true;
    private bool _active;
    private Exception? _failure;
    private TaskCompletionSource? _drained;

    public PersistenceDatabaseTarget Target => _database.Target;

    public PersistenceTransaction(
        MoongatePersistenceService owner,
        PostgreSqlDatabase database,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        _owner = owner;
        _database = database;
        _transaction = transaction;
        _cancellationToken = cancellationToken;
    }

    public Task<T?> GetByIdForUpdateAsync<T>(Serial id, CancellationToken cancellationToken = default)
        where T : class, IMoongateEntity
    {
        return RunAsync<T?>(
            async (orm, transaction, token) =>
            {
                if (!id.IsValid)
                {
                    throw new ArgumentOutOfRangeException(nameof(id));
                }

                if (_owner.GetTarget(typeof(T)) != Target)
                {
                    throw new InvalidOperationException("Transactions cannot cross database targets.");
                }

                try
                {
                    return await orm.Select<T>()
                        .WithTransaction(transaction)
                        .Where(entity => entity.Id == id)
                        .ForUpdate()
                        .ToOneAsync(token)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (token.IsCancellationRequested)
                {
                    // FreeSql wraps provider cancellation; preserve the public cancellation contract.
                    throw new OperationCanceledException("Row-lock read canceled.", exception, token);
                }
            },
            cancellationToken
        );
    }

    public async Task CompleteCallbackAsync()
    {
        Task drain;

        lock (_sync)
        {
            _accepting = false;

            if (_active)
            {
                _failure ??= new InvalidOperationException("The callback returned with an operation still running.");
                drain = (_drained = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            }
            else
            {
                drain = Task.CompletedTask;
            }
        }

        await drain.ConfigureAwait(false);

        lock (_sync)
        {
            if (_failure is not null)
            {
                throw new InvalidOperationException("The persistence transaction failed and cannot commit.", _failure);
            }
        }
    }

    public void Fail(Exception exception)
    {
        lock (_sync)
        {
            _failure ??= exception;
        }
    }

    public IDataAccess<T> GetDataAccess<T>() where T : class, IMoongateEntity
    {
        try
        {
            lock (_sync)
            {
                EnsureUsable();

                if (_owner.GetTarget(typeof(T)) != Target)
                {
                    throw new InvalidOperationException("Transactions cannot cross database targets.");
                }

                return new DataAccess<T>(_owner, this);
            }
        }
        catch (Exception exception)
        {
            Fail(exception);

            throw;
        }
    }

    public async Task<TResult> RunAsync<TResult>(
        Func<IFreeSql, DbTransaction?, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        var admitted = false;

        try
        {
            lock (_sync)
            {
                EnsureUsable();

                if (_active)
                {
                    throw new InvalidOperationException("Concurrent operations on a persistence transaction are forbidden.");
                }

                _active = true;
                admitted = true;
            }

            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
            linkedCancellation.Token.ThrowIfCancellationRequested();

            return await operation(_database.Orm, _transaction, linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Fail(exception);

            throw;
        }
        finally
        {
            if (admitted)
            {
                lock (_sync)
                {
                    _active = false;
                    _drained?.TrySetResult();
                }
            }
        }
    }

    public Task<int> UpsertSnapshotsAsync<T>(T[] snapshots, CancellationToken cancellationToken)
        where T : class, IMoongateEntity
    {
        return RunAsync(
            (orm, transaction, token) =>
            {
                if (_owner.GetTarget(typeof(T)) != Target)
                {
                    throw new InvalidOperationException("Snapshots cannot cross database targets.");
                }

                return orm.InsertOrUpdate<T>().WithTransaction(transaction).SetSource(snapshots).ExecuteAffrowsAsync(token);
            },
            cancellationToken
        );
    }

    private void EnsureUsable()
    {
        if (!_accepting || !_owner.IsCurrentTransaction(this))
        {
            throw new InvalidOperationException("The persistence transaction facade is outside its callback scope.");
        }

        if (_failure is not null)
        {
            throw new InvalidOperationException("The persistence transaction has already failed.", _failure);
        }
    }
}
