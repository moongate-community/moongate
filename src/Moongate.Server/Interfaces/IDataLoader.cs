namespace Moongate.Server.Interfaces;

/// <summary>
/// A startup data-file loader. Implementations load one kind of game data (e.g. skills) and
/// populate the relevant registry. Run once, in priority order, by <see cref="IDataLoaderService" />.
/// </summary>
public interface IDataLoader
{
    /// <summary>Loads this loader's data source into its target registry.</summary>
    ValueTask LoadAsync(CancellationToken ct = default);
}
