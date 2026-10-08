namespace Moongate.UoxItemConverter.Data.Internal.Vendors;

/// <summary>
///     A line a ModernUO <c>SBInfo</c> sells: the C# type of the goods and the numbers beside it.
/// </summary>
internal sealed class ImportedBuyLine
{
    public required string TypeName { get; init; }

    public required int Price { get; init; }

    public required int Amount { get; init; }

    public required int Graphic { get; init; }

    public required int Hue { get; init; }

    /// <summary>
    ///     The name ModernUO gives the line; empty when the goods name themselves.
    /// </summary>
    public string Name { get; init; } = "";
}
