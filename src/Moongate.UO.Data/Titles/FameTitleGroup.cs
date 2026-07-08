namespace Moongate.UO.Data.Titles;

/// <summary>
/// A fame tier: the upper fame threshold and, within it, the karma tiers that select the final title.
/// The last group in the table is the catch-all for the highest fame.
/// </summary>
public sealed class FameTitleGroup
{
    public int Fame { get; set; }
    public List<KarmaTitle> Karma { get; set; } = [];
}
