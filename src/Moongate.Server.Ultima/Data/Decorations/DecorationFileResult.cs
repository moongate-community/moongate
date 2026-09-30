namespace Moongate.Server.Ultima.Data.Decorations;

/// <summary>
///     What decorating one file did: items placed, items already there, and the placements skipped by kind.
/// </summary>
public sealed record DecorationFileResult(
    string Folder,
    string Name,
    int Placed,
    int Present,
    IReadOnlyDictionary<string, int> SkippedByType
)
{
    public int Skipped => SkippedByType.Values.Sum();
}
