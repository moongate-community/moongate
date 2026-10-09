namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     A <c>[[group]]</c> of a craft: a category of its gump and its recipes.
/// </summary>
public class CraftGroup
{
    /// <summary>
    ///     The name the gump shows.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The recipes, in order.
    /// </summary>
    public List<CraftRecipe> Recipe { get; set; } = [];
}
