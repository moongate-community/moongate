using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Internal.Vendors;

/// <summary>
///     The shop window a player has open: the vendor, its virtual shop container, and the line each virtual serial of the
///     window stands for.
/// </summary>
/// <param name="Vendor">The serial of the vendor.</param>
/// <param name="ShopContainer">The virtual serial of the shop container the lines are in.</param>
/// <param name="Lines">The lines of the window, by their virtual serials.</param>
public sealed record VendorWindow(Serial Vendor, Serial ShopContainer, IReadOnlyDictionary<Serial, VendorWindowLine> Lines);
