using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Shops;

namespace Moongate.Server.Ultima.Data.Internal.Vendors;

/// <summary>
///     A line of an open shop window.
/// </summary>
/// <param name="Line">
///     The shop line it shows.
/// </param>
/// <param name="Template">
///     The item template of its goods.
/// </param>
/// <param name="Stock">
///     The shelf the pieces come off.
/// </param>
public sealed record VendorWindowLine(ShopLine Line, ItemTemplate Template, StockLine Stock);
