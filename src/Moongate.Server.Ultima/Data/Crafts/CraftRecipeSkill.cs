namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     Another skill a recipe asks for, besides the craft's own.
/// </summary>
public class CraftRecipeSkill
{
    /// <summary>
    ///     The skill, named as in <c>data/skills.toml</c>.
    /// </summary>
    public string Skill { get; set; } = string.Empty;

    /// <summary>
    ///     The least to try the recipe, in points.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    ///     The most, in points, which the try of the skill is measured against.
    /// </summary>
    public double Max { get; set; }
}
