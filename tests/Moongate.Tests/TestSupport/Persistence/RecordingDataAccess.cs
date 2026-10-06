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

    /// <summary>
    ///     Gets or sets what runs on each upsert before it is stored, such as recording the order across entity types;
    ///     an exception it throws fails that upsert.
    /// </summary>
    public Action<T>? OnUpsert { get; set; }

    /// <summary>
    ///     Gets or sets the next serial <see cref="ReserveSerialAsync" /> hands out.
    /// </summary>
    public uint NextSerial { get; set; } = 0x40000100;

    /// <summary>
    ///     Gets or sets what fails every reservation.
    /// </summary>
    public Exception? FailReservations { get; set; }

    /// <summary>
    ///     Gets how many serials were reserved.
    /// </summary>
    public int Reserved { get; private set; }

    public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Upserted.RemoveAll(entity => entity.Id == id) > 0);
    }

    public Task<Serial> ReserveSerialAsync(CancellationToken cancellationToken = default)
    {
        if (FailReservations is not null)
        {
            return Task.FromException<Serial>(FailReservations);
        }

        lock (Upserted)
        {
            Reserved++;

            return Task.FromResult(new Serial(NextSerial++));
        }
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<T>>(Upserted.ToList());
    }

    public Task<T?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Upserted.LastOrDefault(entity => entity.Id == id));
    }

    public Task<IReadOnlyList<T>> QueryAsync(
        Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default
    )
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

        OnUpsert?.Invoke(entity);

        lock (Upserted)
        {
            Upserted.Add(entity);
        }
    }
}
