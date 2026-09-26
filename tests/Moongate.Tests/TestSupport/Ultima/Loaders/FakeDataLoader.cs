using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Tests.TestSupport.Ultima.Loaders;

/// <summary>
///     An <see cref="IDataLoader{TEntity}" /> whose entities and dependencies come from the container,
///     so
///     <c>
///         AddUltimaDataLoader
///     </c>
///     can construct it the same way it constructs a real loader.
/// </summary>
public sealed class FakeDataLoader<TEntity> : IDataLoader<TEntity>
{
    private readonly IReadOnlyList<TEntity> _entities;
    private readonly List<string> _callLog;

    public FakeDataLoader(IReadOnlyList<TEntity> entities, List<string> callLog)
    {
        _entities = entities;
        _callLog = callLog;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _callLog.Add($"{typeof(TEntity).Name}.Initialize");

        return Task.CompletedTask;
    }

    public Task<DataLoaderResult<TEntity>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        _callLog.Add($"{typeof(TEntity).Name}.Load");

        return Task.FromResult(new DataLoaderResult<TEntity> { Entities = _entities });
    }
}
