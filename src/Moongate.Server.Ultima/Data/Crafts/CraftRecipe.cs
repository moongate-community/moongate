namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     A <c>[[group.recipe]]</c>: an item a craft makes, the skill it asks for and what it takes.
/// </summary>
public class CraftRecipe
{
    /// <summary>
    ///     The name the gump shows.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The item template made.
    /// </summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>
    ///     The least of the main skill to try it, in points.
    /// </summary>
    public double SkillMin { get; set; }

    /// <summary>
    ///     The main skill at which it never fails, in points.
    /// </summary>
    public double SkillMax { get; set; }

    /// <summary>
    ///     What it takes, at least one.
    /// </summary>
    public List<CraftRecipeResource> Resources { get; set; } = [];

    /// <summary>
    ///     Other skills it asks for.
    /// </summary>
    public List<CraftRecipeSkill> Skills { get; set; } = [];
}
