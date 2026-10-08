namespace Moongate.Server.Ultima.Data.Templates.Shops;

/// <summary>
///     A shop: what the vendors that use it sell, and what they buy.
/// </summary>
public class ShopDefinition
{
    /// <summary>
    ///     The stable id of the shop.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     The ids of the mobile templates whose vendors use this shop. A template is in one shop only.
    /// </summary>
    public List<string> Vendors { get; set; } = [];

    /// <summary>
    ///     The lines the vendor sells to a player.
    /// </summary>
    public List<ShopLine> Buy { get; set; } = [];

    /// <summary>
    ///     The lines the vendor buys from a player.
    /// </summary>
    public List<ShopLine> Sell { get; set; } = [];
}
