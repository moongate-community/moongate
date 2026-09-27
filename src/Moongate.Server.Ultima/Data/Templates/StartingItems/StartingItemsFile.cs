namespace Moongate.Server.Ultima.Data.Templates.StartingItems;

/// <summary>
///     <c>data/starting_items.toml</c>: a <c>[[set]]</c> array of <see cref="StartingItemSet" />.
/// </summary>
public class StartingItemsFile
{
    /// <summary>
    ///     The sets in the file.
    /// </summary>
    public List<StartingItemSet> Set { get; set; } = [];
}
