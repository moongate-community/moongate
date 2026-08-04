namespace Moongate.Http.Plugin.Data.Api.Characters;

/// <summary>
/// One item as the API reports it, with whatever it contains. Deliberately not <c>ItemEntity</c>, which
/// carries script and loot ids: a field added to the entity later would publish itself.
/// </summary>
/// <param name="Serial">The item's serial, as <c>0x40000001</c>.</param>
/// <param name="Name">
/// What to call the item: whatever it was renamed to, else its template's name, else the client's own
/// word for that graphic. Almost nothing in UO stores a name, so the third case is the common one.
/// </param>
/// <param name="TemplateId">The template it was built from.</param>
/// <param name="ItemId">The art id, which addresses <c>/api/v1/images/items/{id}.png</c>.</param>
/// <param name="Hue">0 for the raw art.</param>
/// <param name="Amount">How many, for a stack.</param>
/// <param name="Layer">The layer it is worn on, or null when it sits inside a container.</param>
/// <param name="Contents">What it contains. Empty for anything that is not a container.</param>
public sealed record CharacterItemResponse(
    string Serial,
    string Name,
    string TemplateId,
    int ItemId,
    int Hue,
    int Amount,
    string? Layer,
    IReadOnlyList<CharacterItemResponse> Contents
);
