using Moongate.Server.Ultima.Data.Events;

namespace Moongate.Server.Ultima.Characters;

/// <summary>
///     What Lua scripts receive for the character events published to them.
/// </summary>
public static class CharacterScriptEvents
{
    /// <summary>
    ///     The fields of <c>character_created</c>: the character's serial and account, name, race and gender (as numbers),
    ///     and where it starts.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> CharacterCreated(CharacterCreatedEvent evt)
    {
        var character = evt.Character;

        return new Dictionary<string, object?>
        {
            ["serial"] = (long)character.Id.Value,
            ["account_id"] = character.AccountId is { } account ? (long)account.Value : null,
            ["name"] = character.Name,
            ["race"] = character.Race,
            ["gender"] = character.Gender,
            ["map"] = character.Map,
            ["x"] = character.X,
            ["y"] = character.Y,
            ["z"] = character.Z
        };
    }
}
