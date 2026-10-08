using Moongate.Server.Ultima.Data.Templates.Shops;

namespace Moongate.Server.Ultima.Data.Internal.Vendors;

/// <summary>
///     Goods a player sold to a vendor, offered again until <see cref="ExpiresAt" />.
/// </summary>
public sealed class ResaleLine
{
    /// <summary>
    ///     The line as the shop window shows it: the item template, the price of a piece, the hue and the name.
    /// </summary>
    public required ShopLine Line { get; init; }

    /// <summary>
    ///     The pieces on the shelf.
    /// </summary>
    public required StockLine Stock { get; init; }

    /// <summary>
    ///     When the vendor no longer offers them.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
