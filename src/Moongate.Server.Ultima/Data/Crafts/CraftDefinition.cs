namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     One craft of <c>data/crafts</c>, a file each: its skill, its sound and the groups of recipes of its gump.
/// </summary>
public class CraftDefinition
{
    /// <summary>
    ///     The name scripts open it by, a lower-case identifier such as <c>carpentry</c>.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The name its gump shows.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The main skill of every recipe, named as in <c>data/skills.toml</c>.
    /// </summary>
    public string Skill { get; set; } = string.Empty;

    /// <summary>
    ///     The sound played at each stroke.
    /// </summary>
    public int Sound { get; set; }

    /// <summary>
    ///     The groups of the gump, in order.
    /// </summary>
    public List<CraftGroup> Group { get; set; } = [];
}
