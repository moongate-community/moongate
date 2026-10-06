using Moongate.Server.Ultima.Data.Events;

namespace Moongate.Server.Ultima.Characters;

/// <summary>
///     What Lua scripts receive for the character events published to them.
/// </summary>
public static class CharacterScriptEvents
{
    /// <summary>
    ///     The fields of <c>character_created</c>: the character's serial and account, name, race and gender (as
    ///     numbers),
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

    /// <summary>
    ///     The fields of <c>character_deletion_requested</c>: the character's serial, account and name.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> CharacterDeletionRequested(CharacterDeletionRequestedEvent evt)
    {
        var character = evt.Character;

        return new Dictionary<string, object?>
        {
            ["serial"] = (long)character.Id.Value,
            ["account_id"] = character.AccountId is { } account ? (long)account.Value : null,
            ["name"] = character.Name
        };
    }

    /// <summary>
    ///     The fields of <c>character_entered_world</c>: the character's serial, account and name, and where it is.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> CharacterEnteredWorld(CharacterEnteredWorldEvent evt)
    {
        var character = evt.Character;

        return new Dictionary<string, object?>
        {
            ["serial"] = (long)character.Id.Value,
            ["account_id"] = character.AccountId is { } account ? (long)account.Value : null,
            ["name"] = character.Name,
            ["map"] = character.Map,
            ["x"] = character.X,
            ["y"] = character.Y,
            ["z"] = character.Z
        };
    }

    /// <summary>
    ///     The fields of <c>character_left_world</c>: the character's serial, account and name, and where it left.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> CharacterLeftWorld(CharacterLeftWorldEvent evt)
    {
        var character = evt.Character;

        return new Dictionary<string, object?>
        {
            ["serial"] = (long)character.Id.Value,
            ["account_id"] = character.AccountId is { } account ? (long)account.Value : null,
            ["name"] = character.Name,
            ["map"] = character.Map,
            ["x"] = character.X,
            ["y"] = character.Y,
            ["z"] = character.Z
        };
    }

    /// <summary>
    ///     The fields of <c>player_say</c>: the speaker's serial and name, the text as the others heard it and how
    ///     it was said, a <c>SpeechType</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> PlayerSay(PlayerSaidEvent evt)
    {
        return new Dictionary<string, object?>
        {
            ["serial"] = (long)evt.Speaker.Id.Value,
            ["name"] = evt.Speaker.Name,
            ["text"] = evt.Text,
            ["type"] = (long)evt.Type
        };
    }

    /// <summary>
    ///     The fields of <c>player_region_changed</c>: the character's serial and name, the names of the region it left
    ///     and of the one it is in (nil outside every region), and where it stands.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> PlayerRegionChanged(PlayerRegionChangedEvent evt)
    {
        var player = evt.Player;

        return new Dictionary<string, object?>
        {
            ["serial"] = (long)player.Id.Value,
            ["name"] = player.Name,
            ["previous"] = evt.Previous?.Name,
            ["current"] = evt.Current?.Name,
            ["map"] = player.Map,
            ["x"] = player.X,
            ["y"] = player.Y,
            ["z"] = player.Z
        };
    }
}
