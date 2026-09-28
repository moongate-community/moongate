using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     The outcome of a deletion request: the character now pending deletion and the list the client should show, or
///     why it was refused.
/// </summary>
public sealed record CharacterDeletionResult(
    MobileEntity? Character,
    IReadOnlyList<string?> Names,
    CharacterDeleteResultType? Refusal
)
{
    public static CharacterDeletionResult Deleted(MobileEntity character, IReadOnlyList<string?> names)
    {
        return new(character, names, null);
    }

    public static CharacterDeletionResult Refused(CharacterDeleteResultType refusal)
    {
        return new(null, [], refusal);
    }
}
