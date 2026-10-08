using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads every <c>*.toml</c> under <c>templates/shops/</c>, recursively. A shop with no id, a duplicate shop id, a
///     line whose item is not an item template, a price or an amount below 1, a vendor that is not a mobile template
///     and a vendor in two shops stop the server at startup, naming the file and the shop.
/// </summary>
public class ShopsLoader : IDataLoader<ShopDefinition>
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<ShopsLoader>();

    private string shopsDirectoryPath => Path.Join(_directoriesConfig["templates"], "shops");

    public ShopsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<ShopDefinition>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var itemIds = _dataLoaderService.GetEntities<ItemTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var mobileIds = _dataLoaderService.GetEntities<MobileTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var shops = new Dictionary<string, (ShopDefinition Shop, string File)>(StringComparer.Ordinal);
        var shopOfVendor = new Dictionary<string, string>(StringComparer.Ordinal);

        if (Directory.Exists(shopsDirectoryPath))
        {
            foreach (var path in Directory.EnumerateFiles(shopsDirectoryPath, "*.toml", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                var file = await TomlUtils.DeserializeFromFileAsync<ShopFile>(path, null, cancellationToken) ??
                           new ShopFile();

                foreach (var shop in file.Shop)
                {
                    if (string.IsNullOrWhiteSpace(shop.Id))
                    {
                        throw new InvalidDataException($"{path}: a shop has no id.");
                    }

                    if (!shops.TryAdd(shop.Id, (shop, path)))
                    {
                        throw new InvalidDataException(
                            $"Shop id '{shop.Id}' is defined in both {shops[shop.Id].File} and {path}."
                        );
                    }

                    Check(shop, path, itemIds, mobileIds, shopOfVendor);
                }
            }
        }

        _logger.Information("Found {Count} shops", shops.Count);

        return new() { Entities = shops.Values.Select(pair => pair.Shop).ToList() };
    }

    private static void Check(
        ShopDefinition shop,
        string file,
        HashSet<string> itemIds,
        HashSet<string> mobileIds,
        Dictionary<string, string> shopOfVendor
    )
    {
        var where = $"{file}: shop '{shop.Id}'";

        foreach (var vendor in shop.Vendors)
        {
            if (!mobileIds.Contains(vendor))
            {
                throw new InvalidDataException($"{where} names the vendor '{vendor}', which is not a mobile template.");
            }

            if (!shopOfVendor.TryAdd(vendor, shop.Id))
            {
                throw new InvalidDataException(
                    $"{where} names the vendor '{vendor}', which is already in shop '{shopOfVendor[vendor]}'."
                );
            }
        }

        foreach (var (kind, lines) in new[] { ("buy", shop.Buy), ("sell", shop.Sell) })
        {
            foreach (var line in lines)
            {
                if (!itemIds.Contains(line.Item ?? ""))
                {
                    throw new InvalidDataException(
                        $"{where} has a {kind} line for '{line.Item}', which is not an item template."
                    );
                }

                if (line.Price < 1)
                {
                    throw new InvalidDataException($"{where} has a {kind} line for '{line.Item}' with a price below 1.");
                }

                if (line.Amount < 1)
                {
                    throw new InvalidDataException($"{where} has a {kind} line for '{line.Item}' with an amount below 1.");
                }
            }
        }
    }
}
