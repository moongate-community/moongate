namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     One entry of an <see cref="NpcListTemplate" />: a mobile template or another list, picked in proportion to its
///     weight.
/// </summary>
public class NpcListEntry
{
    /// <summary>
    ///     How likely the entry is picked against the others of its list; 1 by default.
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>
    ///     The mobile template the entry spawns.
    /// </summary>
    public string? MobileId { get; set; }

    /// <summary>
    ///     The list the entry picks from instead, such as <c>all_trolls</c>.
    /// </summary>
    public string? NpcListId { get; set; }
}
