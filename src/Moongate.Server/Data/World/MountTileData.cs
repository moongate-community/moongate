namespace Moongate.Server.Data.World;

/// <summary>
/// Holds the mounted display item ids loaded from uoconvert.cfg.
/// </summary>
public sealed class MountTileData
{
    private readonly HashSet<int> _itemIds = [];

    /// <summary>
    /// Gets the loaded mount item ids.
    /// </summary>
    public IReadOnlySet<int> ItemIds => _itemIds;

    /// <summary>
    /// Returns whether the mounted display item id is known as mountable.
    /// </summary>
    public bool Contains(int itemId)
        => _itemIds.Contains(itemId);

    /// <summary>
    /// Replaces the loaded mount item ids.
    /// </summary>
    public void Replace(IEnumerable<int> itemIds)
    {
        _itemIds.Clear();

        foreach (var itemId in itemIds.Distinct())
        {
            _itemIds.Add(itemId);
        }
    }
}
