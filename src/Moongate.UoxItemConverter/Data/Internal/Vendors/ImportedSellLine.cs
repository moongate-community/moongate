namespace Moongate.UoxItemConverter.Data.Internal.Vendors;

/// <summary>
///     A type an
///     <c>
///         SBInfo
///     </c>
///     buys from players and the gold it pays for one.
/// </summary>
internal sealed class ImportedSellLine
{
    public required string TypeName { get; init; }

    public required int Price { get; init; }
}
