namespace Moongate.Server.Ultima.Data.Templates.Shops;

/// <summary>
///     One line of a shop: an item template, its price and, for the lines a vendor sells, how many it stocks.
/// </summary>
public class ShopLine
{
    /// <summary>
    ///     The id of the item template of the goods.
    /// </summary>
    public string Item { get; set; }

    /// <summary>
    ///     The price of one piece in gold. At least 1.
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    ///     How many pieces the vendor starts with. At least 1; the lines a vendor buys ignore it.
    /// </summary>
    public int Amount { get; set; } = 1;

    /// <summary>
    ///     The hue of the goods. 0 keeps the one of the item template.
    /// </summary>
    public int Hue { get; set; }

    /// <summary>
    ///     The name shown in the window. Empty: the cliloc of the graphic.
    /// </summary>
    public string Name { get; set; } = "";
}
