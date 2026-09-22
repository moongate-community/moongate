namespace Moongate.Server.Ultima.Data;

/// <summary>What one <see cref="Interfaces.Loaders.IDataLoader{TEntity}"/> run produced.</summary>
public sealed class DataLoaderResult<TEntity>
{
    /// <summary>Gets every entity the loader read.</summary>
    public required IReadOnlyList<TEntity> Entities { get; init; }
}
