using System.Linq.Expressions;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Keeps upserted entities in memory, or fails every upsert, and lets a test hold an upsert until it says go.
/// </summary>
public sealed class RecordingDataAccess<T> : IDataAccess<T> where T : class, IMoongateEntity
{
    public List<T> Upserted { get; } = [];

    public Exception? FailUpserts { get; set; }

    public Task? HoldUpserts { get; set; }

    public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Upserted.RemoveAll(entity => entity.Id == id) > 0);
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<T>>(Upserted.ToList());
    }

    public Task<T?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Upserted.LastOrDefault(entity => entity.Id == id));
    }

    public Task<IReadOnlyList<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<T>>(Upserted.Where(predicate.Compile()).ToList());
    }

    public Task<IReadOnlyList<T>> QueryAsync(
        Expression<Func<T, bool>> predicate,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<T>>(Upserted.Where(predicate.Compile()).Skip(skip).Take(take).ToList());
    }

    public async Task UpsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (HoldUpserts is not null)
        {
            await HoldUpserts;
        }

        if (FailUpserts is not null)
        {
            throw FailUpserts;
        }

        lock (Upserted)
        {
            Upserted.Add(entity);
        }
    }
}
