using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Finds the shop a vendor uses, from the shops <see cref="Loaders.ShopsLoader" /> loaded.
/// </summary>
public interface IShopService
{
    /// <summary>
    ///     Finds the shop of the vendors made from a mobile template.
    /// </summary>
    /// <param name="mobileTemplateId">
    ///     The id of the mobile template.
    /// </param>
    /// <param name="shop">
    ///     The shop, when there is one.
    /// </param>
    /// <returns>
    ///     True when the template has a shop.
    /// </returns>
    bool TryGetFor(string mobileTemplateId, [NotNullWhen(true)] out ShopDefinition? shop);

    /// <summary>
    ///     Finds the shop of a vendor, by the template it was made from.
    /// </summary>
    /// <param name="vendor">
    ///     The vendor.
    /// </param>
    /// <param name="shop">
    ///     The shop, when there is one.
    /// </param>
    /// <returns>
    ///     True when the vendor has a shop.
    /// </returns>
    bool TryGetFor(MobileEntity vendor, [NotNullWhen(true)] out ShopDefinition? shop);
}
