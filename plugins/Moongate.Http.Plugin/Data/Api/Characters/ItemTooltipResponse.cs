namespace Moongate.Http.Plugin.Data.Api.Characters;

/// <summary>What the game itself says about one item, plus what the portal knows about it.</summary>
/// <param name="Serial">The item's serial, as <c>0x40000001</c>.</param>
/// <param name="Lines">
/// The object property list as the game client renders it, resolved to text. Empty when the shard's
/// string table is not loaded — the technical fields are still worth showing, so that is not an error.
/// </param>
/// <param name="TemplateId">The template it was built from.</param>
/// <param name="ItemId">The art id.</param>
/// <param name="Hue">0 for the raw art.</param>
/// <param name="Amount">How many, for a stack.</param>
public sealed record ItemTooltipResponse(
    string Serial,
    IReadOnlyList<string> Lines,
    string TemplateId,
    int ItemId,
    int Hue,
    int Amount
);
