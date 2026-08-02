namespace Moongate.Http.Plugin.Data.Api.Characters;

/// <summary>Everything one character is: who they are, what they wear, what they carry.</summary>
/// <param name="Character">The same shape the character lists report, so the two cannot disagree.</param>
/// <param name="Equipment">Worn items by layer, the backpack and the bank box among them.</param>
/// <param name="Backpack">The backpack's contents, nested containers expanded.</param>
/// <param name="Skills">
/// The skills the character has, by name. Sparse on purpose: a mobile never stores a skill left at
/// zero, so this is what the character actually trained rather than the whole catalogue.
/// </param>
public sealed record CharacterDetailResponse(
    CharacterResponse Character,
    IReadOnlyList<CharacterItemResponse> Equipment,
    IReadOnlyList<CharacterItemResponse> Backpack,
    IReadOnlyList<CharacterSkillResponse> Skills
);
