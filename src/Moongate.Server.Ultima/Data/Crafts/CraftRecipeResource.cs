namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     What a recipe takes: a list of <c>data/crafts/resources.toml</c> or an item template, and how many.
/// </summary>
public class CraftRecipeResource
{
    /// <summary>
    ///     The id of a resource list, such as <c>wood</c>, or of an item template.
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    ///     How many units, at least 1.
    /// </summary>
    public int Amount { get; set; }
}
