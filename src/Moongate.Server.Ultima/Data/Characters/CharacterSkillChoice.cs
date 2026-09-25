using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     One starting skill a player chose at character creation, with its value in whole points.
/// </summary>
public readonly record struct CharacterSkillChoice
{
    public SkillType Skill { get; init; }

    public byte Value { get; init; }
}
