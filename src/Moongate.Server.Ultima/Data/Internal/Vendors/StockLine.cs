namespace Moongate.Server.Ultima.Data.Internal.Vendors;

/// <summary>
///     The pieces a vendor has of one line of its shop, and the most it stocks.
/// </summary>
public sealed class StockLine
{
    /// <summary>
    ///     How many pieces are on the shelf now.
    /// </summary>
    public int Current { get; set; }

    /// <summary>
    ///     How many pieces the shelf is filled to at a restock.
    /// </summary>
    public int Max { get; set; }
}
