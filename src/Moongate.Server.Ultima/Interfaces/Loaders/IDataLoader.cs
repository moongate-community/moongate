using Moongate.Server.Ultima.Data;

namespace Moongate.Server.Ultima.Interfaces.Loaders;

/// <summary>Loads every <typeparamref name="TEntity"/> a shard needs at startup.</summary>
/// <remarks>
/// Register with <c>AddUltimaDataLoader&lt;TLoader, TEntity&gt;</c>; <see cref="Services.DataLoaderService"/>
/// then calls <see cref="InitializeAsync"/> followed by <see cref="LoadDataAsync"/> once, in priority order,
/// and keeps the result under its own type for <c>GetEntities&lt;TEntity&gt;()</c>.
/// </remarks>
public interface IDataLoader<TEntity>
{
    /// <summary>Prepares the loader, for example opening the files or connections it will read from.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads every entity this loader is responsible for.</summary>
    Task<DataLoaderResult<TEntity>> LoadDataAsync(CancellationToken cancellationToken = default);
}
