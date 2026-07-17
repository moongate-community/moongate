using Moongate.Persistence.Entities;

namespace Moongate.Server.Data.Api;

/// <summary>
/// A character as the API reports it. Deliberately not <c>MobileEntity</c>, which carries BrainScriptId,
/// LootTableId and BackpackId: returning the entity would publish the shard's internals to every caller,
/// and a field added to the entity later would publish itself. Naming the fields here means that cannot
/// happen by default — the same reason <see cref="AccountResponse" /> exists rather than returning
/// AccountEntity with its PasswordHash.
/// </summary>
/// <param name="Serial">The character's serial, as <c>0x40000001</c>.</param>
/// <param name="AccountUsername">The owning account. Null on the player's own route, where it is the caller.</param>
public sealed record CharacterResponse(
    string Serial,
    string Name,
    string? AccountUsername,
    string Race,
    string Gender,
    int Body,
    int Strength,
    int Dexterity,
    int Intelligence,
    int Hits,
    int HitsMax,
    int Stamina,
    int StaminaMax,
    int Mana,
    int ManaMax,
    int Kills,
    int SkinHue,
    int HairStyle,
    int HairHue,
    int MapId,
    int X,
    int Y,
    int Z
)
{
    /// <summary>
    /// Projects a mobile. Pass <paramref name="accountUsername" /> as null on the player's own route:
    /// telling callers who they are is noise, and the admin route is the one that needs the owner.
    /// </summary>
    public static CharacterResponse From(MobileEntity mobile, string? accountUsername)
        => new(
            mobile.Id.ToString(),
            mobile.Name,
            accountUsername,
            mobile.Race.ToString(),
            mobile.Gender.ToString(),
            mobile.Body,
            mobile.Strength,
            mobile.Dexterity,
            mobile.Intelligence,
            mobile.Hits,
            mobile.HitsMax,
            mobile.Stamina,
            mobile.StaminaMax,
            mobile.Mana,
            mobile.ManaMax,
            mobile.Kills,
            mobile.SkinHue.Value,
            mobile.HairStyle,
            mobile.HairHue.Value,
            mobile.MapId,
            mobile.Position.X,
            mobile.Position.Y,
            mobile.Position.Z
        );
}
