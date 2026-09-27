using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     The outcome of a character creation: the saved character and its starting items, or why it was refused.
/// </summary>
public sealed record CharacterCreationResult(
    MobileEntity? Character,
    IReadOnlyList<ItemEntity> Items,
    CharacterCreationRefusalType? Refusal
)
{
    /// <summary>
    ///     Gets whether the character was created and saved.
    /// </summary>
    public bool IsCreated => Character is not null;

    public static CharacterCreationResult Created(MobileEntity character, IReadOnlyList<ItemEntity> items)
    {
        return new(character, items, null);
    }

    public static CharacterCreationResult Refused(CharacterCreationRefusalType refusal)
    {
        return new(null, [], refusal);
    }
}
