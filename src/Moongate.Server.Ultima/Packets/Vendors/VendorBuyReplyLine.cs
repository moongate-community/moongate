using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     A line of what a player chose in a shop window.
/// </summary>
/// <param name="Layer">
///     The layer the client names, the shop container's.
/// </param>
/// <param name="Item">
///     The virtual serial of the line.
/// </param>
/// <param name="Amount">
///     How many pieces.
/// </param>
public readonly record struct VendorBuyReplyLine(byte Layer, Serial Item, ushort Amount);
