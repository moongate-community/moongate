using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     An item a player chose to sell and how many pieces of it.
/// </summary>
/// <param name="Item">
///     The serial of the item.
/// </param>
/// <param name="Amount">
///     How many pieces.
/// </param>
public readonly record struct VendorSellReplyLine(Serial Item, ushort Amount);
