namespace Moongate.Server.Ultima.Types.Training;

/// <summary>
///     What a trainer can do for a player at a skill.
/// </summary>
public enum TrainingResultType
{
    /// <summary>
    ///     There are points to teach.
    /// </summary>
    Ok,

    /// <summary>
    ///     The trainer does not teach the skill, or the player cannot be taught: dead, or an NPC.
    /// </summary>
    NotTeaching,

    /// <summary>
    ///     The player knows more than the trainer would teach.
    /// </summary>
    KnowsMore,

    /// <summary>
    ///     The player knows all the trainer would teach.
    /// </summary>
    KnowsAll,

    /// <summary>
    ///     The skill is not locked up, or the total cap leaves no room even after lowering the skills locked down.
    /// </summary>
    NotRaisable
}
