using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     What a client asks for when it creates a character, the same for the classic (0xF8) and the Enhanced Client
///     (0x8D) packets. The values are raw: <see cref="Services.CharacterService" /> refuses or sanitizes them.
/// </summary>
public sealed record CharacterCreationRequest
{
    /// <summary>
    ///     The character-list slot to fill, counted from 0.
    /// </summary>
    public required int Slot { get; init; }

    public required string Name { get; init; }

    /// <summary>
    ///     The profession id of <c>professions.toml</c>; 0 is the "Advanced" choice, where the player picked stats and
    ///     skills.
    /// </summary>
    public required int Profession { get; init; }

    /// <summary>
    ///     The index of the chosen city in <c>starting_cities.toml</c>, as the character list sent it.
    /// </summary>
    public required int StartingCity { get; init; }

    public required GenderType Gender { get; init; }

    public required RaceType Race { get; init; }

    public required int Strength { get; init; }

    public required int Dexterity { get; init; }

    public required int Intelligence { get; init; }

    /// <summary>
    ///     The skills chosen for the "Advanced" profession, in whole points.
    /// </summary>
    public required IReadOnlyList<CharacterSkillChoice> Skills { get; init; }

    public required Hue SkinHue { get; init; }

    /// <summary>
    ///     The item id of the hair style; 0 means no hair.
    /// </summary>
    public required int HairStyle { get; init; }

    public required Hue HairHue { get; init; }

    /// <summary>
    ///     The item id of the beard style; 0 means no beard.
    /// </summary>
    public required int BeardStyle { get; init; }

    public required Hue BeardHue { get; init; }

    public required Hue ShirtHue { get; init; }

    /// <summary>
    ///     The pants hue; 0 keeps the template's, which is what the Enhanced Client sends since it has no pants choice.
    /// </summary>
    public required Hue PantsHue { get; init; }
}
