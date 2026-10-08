using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Internal.Vendors;

/// <summary>
///     The sell list a player has open: the vendor and the price of a piece of each item that was offered.
/// </summary>
/// <param name="Vendor">
///     The serial of the vendor.
/// </param>
/// <param name="Prices">
///     The gold for one piece, by the serial of each item offered.
/// </param>
public sealed record VendorSellWindow(Serial Vendor, IReadOnlyDictionary<Serial, int> Prices);
