using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     An item of the list a vendor offers to buy: the player's own item, how many of it and the price of a piece.
/// </summary>
/// <param name="Item">The serial of the item.</param>
/// <param name="ItemId">The graphic.</param>
/// <param name="Hue">The hue.</param>
/// <param name="Amount">How many pieces the player has.</param>
/// <param name="Price">The gold the vendor pays for one piece.</param>
/// <param name="Name">The name shown.</param>
public readonly record struct VendorSellListEntry(Serial Item, int ItemId, ushort Hue, int Amount, int Price, string Name);
