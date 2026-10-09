namespace Moongate.Server.Ultima.Data.Templates.Shops;

/// <summary>
///     One file under <c>templates/shops/</c>: a <c>[[shop]]</c> array of <see cref="ShopDefinition" />.
/// </summary>
public class ShopFile
{
    /// <summary>
    ///     The shops in the file.
    /// </summary>
    public List<ShopDefinition> Shop { get; set; } = [];
}
