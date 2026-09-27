using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Templates.StartingItems;

/// <summary>
///     Items a new character gets when it matches every filter set here; a set with no filter goes to everyone.
/// </summary>
public class StartingItemSet
{
    /// <summary>
    ///     Gives the set to characters that start with this skill among their best ones; unset ignores skills.
    /// </summary>
    public SkillType? Skill { get; set; }

    /// <summary>
    ///     Gives the set to characters of this race only; unset gives it to every race.
    /// </summary>
    public RaceType? Race { get; set; }

    /// <summary>
    ///     Gives the set to characters of this gender only; unset gives it to both.
    /// </summary>
    public GenderType? Gender { get; set; }

    /// <summary>
    ///     The items of the set.
    /// </summary>
    public List<StartingItemEntry> Items { get; set; } = [];
}
