using System.Data.Common;
using System.Linq.Expressions;
using FreeSql;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Services;

namespace Moongate.Persistence.DataAccess;

/// <summary>A typed detached facade. Its owner alone manages the shared ORM lifetime.</summary>
public sealed class DataAccess<T> : IDataAccess<T> where T : class, IMoongateEntity
{
    private readonly MoongatePersistenceService _owner;
    private readonly PersistenceTransaction? _transaction;

    internal DataAccess(MoongatePersistenceService owner, PersistenceTransaction? transaction = null)
    {
        _owner = owner;
        _transaction = transaction;
    }

    /// <inheritdoc />
    public Task<T?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
    {
        return RunAsync(false, async (orm, transaction, token) =>
        {
            ValidateId(id);
            return await orm.Select<T>().WithTransaction(transaction).Where(entity => entity.Id == id)
                .ToOneAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync<IReadOnlyList<T>>(false, async (orm, transaction, token) =>
            await orm.Select<T>().WithTransaction(transaction).ToListAsync(token).ConfigureAwait(false), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return QueryCoreAsync(predicate, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> QueryAsync(Expression<Func<T, bool>> predicate, int skip, int take, CancellationToken cancellationToken = default)
    {
        return QueryCoreAsync(predicate, skip, take, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        await RunAsync(true, (orm, transaction, token) =>
        {
            ArgumentNullException.ThrowIfNull(entity);
            ValidateId(entity.Id);
            return orm.InsertOrUpdate<T>().WithTransaction(transaction).SetSource(entity).ExecuteAffrowsAsync(token);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        return RunAsync(true, async (orm, transaction, token) =>
        {
            ValidateId(id);
            return await orm.Delete<T>().WithTransaction(transaction).Where(entity => entity.Id == id)
                .ExecuteAffrowsAsync(token).ConfigureAwait(false) != 0;
        }, cancellationToken);
    }

    private Task<IReadOnlyList<T>> QueryCoreAsync(Expression<Func<T, bool>> predicate, int? skip, int? take, CancellationToken cancellationToken)
    {
        return RunAsync<IReadOnlyList<T>>(false, async (orm, transaction, token) =>
        {
            ArgumentNullException.ThrowIfNull(predicate);
            if (skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skip));
            }

            if (take <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(take));
            }

            var normalized = (Expression<Func<T, bool>>)new SerialConstantExpressionVisitor().Visit(predicate)!;
            var query = orm.Select<T>().WithTransaction(transaction).Where(normalized);
            if (skip.HasValue)
            {
                query = query.OrderBy(entity => entity.Id).Skip(skip.Value).Limit(take!.Value);
            }

            var sql = query.ToSql();
            if (!sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("The predicate did not produce a SQL filter.");
            }

            return await query.ToListAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private Task<TResult> RunAsync<TResult>(bool mutation, Func<IFreeSql, DbTransaction?, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        return _transaction is null
            ? _owner.RunOperationAsync<T, TResult>(mutation, operation, cancellationToken)
            : _transaction.RunAsync(operation, cancellationToken);
    }

    private static void ValidateId(Serial id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Persistence identities must be nonzero.");
        }
    }
}
