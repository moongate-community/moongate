namespace Moongate.UoxItemConverter.Data.Internal.Vendors;

/// <summary>
///     A vendor class of ModernUO: its name and the <c>SBInfo</c> classes it always uses.
/// </summary>
internal sealed class ImportedVendor
{
    public required string Name { get; init; }

    public required List<string> SbInfos { get; init; }
}
