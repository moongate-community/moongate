namespace Moongate.Http.Plugin.Data.Api.Characters;

/// <summary>One skill a character has, as the API reports it.</summary>
/// <param name="Id">The skill id, which is what the mobile stores it under.</param>
/// <param name="Name">The skill's name, or its id when no definition is registered for it.</param>
/// <param name="Value">
/// The skill's value in points — 50.0, not the 500 tenths the entity stores. The tenths are a storage
/// detail, and reporting them would leave every consumer to remember to divide.
/// </param>
/// <param name="Cap">That skill's personal ceiling, in points. 100.0 unless something raised it.</param>
/// <param name="Lock">
/// Whether the skill may drift as it is used: <c>Up</c>, <c>Down</c> or <c>Locked</c> — the arrow the
/// client shows beside each entry.
/// </param>
public sealed record CharacterSkillResponse(int Id, string Name, double Value, double Cap, string Lock);
