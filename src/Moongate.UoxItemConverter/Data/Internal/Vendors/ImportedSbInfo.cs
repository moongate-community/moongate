namespace Moongate.UoxItemConverter.Data.Internal.Vendors;

/// <summary>
///     One <c>SBInfo</c> class of ModernUO: its name and the lines it sells.
/// </summary>
internal sealed class ImportedSbInfo
{
    public required string Name { get; init; }

    public required List<ImportedBuyLine> Lines { get; init; }

    public List<ImportedSellLine> Sells { get; init; } = [];
}
