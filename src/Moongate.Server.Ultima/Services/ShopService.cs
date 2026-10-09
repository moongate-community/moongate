using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Serves the shops <see cref="Loaders.ShopsLoader" /> loaded, indexed by vendor template, the first time one is asked
///     for.
/// </summary>
public class ShopService : IShopService
{
    private readonly Lazy<FrozenDictionary<string, ShopDefinition>> _shopsByVendor;

    public ShopService(IDataLoaderService dataLoaderService)
    {
        _shopsByVendor = new(() => dataLoaderService.GetEntities<ShopDefinition>()
            .SelectMany(shop => shop.Vendors.Select(vendor => (Vendor: vendor, Shop: shop)))
            .ToFrozenDictionary(pair => pair.Vendor, pair => pair.Shop, StringComparer.Ordinal)
        );
    }

    public bool TryGetFor(string mobileTemplateId, [NotNullWhen(true)] out ShopDefinition? shop)
    {
        return _shopsByVendor.Value.TryGetValue(mobileTemplateId, out shop);
    }

    public bool TryGetFor(MobileEntity vendor, [NotNullWhen(true)] out ShopDefinition? shop)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        if (vendor.TemplateId is { } templateId)
        {
            return TryGetFor(templateId, out shop);
        }

        shop = null;

        return false;
    }
}
