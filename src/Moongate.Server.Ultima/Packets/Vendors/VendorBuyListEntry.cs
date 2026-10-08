namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     A line of the list of a shop window: the price of a piece and its name, a cliloc number as text or plain text.
/// </summary>
/// <param name="Price">
///     The price of one piece in gold.
/// </param>
/// <param name="Name">
///     The name shown.
/// </param>
public readonly record struct VendorBuyListEntry(int Price, string Name);
