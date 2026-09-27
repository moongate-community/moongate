using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Templates.StartingItems;

/// <summary>
///     Items a new character gets: every character when <see cref="Common" /> is true, else those matching every filter
///     set here.
/// </summary>
public class StartingItemSet
{
    /// <summary>
    ///     Gives the set to every character, whatever the filters say.
    /// </summary>
    public bool Common { get; set; }

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
