using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.UoxItemConverter.Data.Internal.Vendors;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts the shops of ModernUO ( <c>Mobiles/Vendors/SBInfo/*.cs</c> and the vendor classes in
///     <c>Mobiles/Vendors/NPC</c>) into <c>templates/shops/&lt;vendor&gt;.toml</c>, one file a vendor class. Nothing is
///     compiled or run: the C# is read as syntax. A type becomes the item template with the graphic ModernUO gives the
///     line; a type with no template is dropped and counted in the report.
/// </summary>
internal static class ModernUoVendorConverter
{
    private const string VendorsFolder = "Vendors";
    private const string MobilesFolder = "Mobiles";
    private const int RegexTimeoutSeconds = 1;

    // The templates of an armor or weapon graphic: one for each era, which are all the plain piece, then one for each
    // material. The era the data sets use first is the one chosen when a single template is needed.
    private static readonly string[] Eras = ["lbr", "aos", "t2a", "tol"];

    // The codes of the materials in the ids of the data sets: agapite, bronze, copper, dull copper, gold, shadow,
    // valorite, verite.
    private static readonly string[] Materials = ["a", "b", "c", "d", "g", "s", "va", "ve"];

    private const string Header =
        """
        # What it is for:
        #   The shop of one kind of vendor: what its vendors sell to a player, with the price of a piece and
        #   how many they start with. Every vendor template listed in vendors uses it.
        #
        # Fields:
        #   [[shop]]      one shop
        #     id          the stable id of the shop
        #     vendors     the ids of the mobile templates whose vendors use the shop (a template is in one shop)
        #   [[shop.buy]]  one line the vendor sells
        #     item        the id of an item template
        #     price       gold for one piece, at least 1
        #     amount      how many pieces the vendor starts with, at least 1
        #     hue         the hue of the goods; 0 keeps the one of the item template
        #     name        the name shown in the shop window; empty: the client's name of the graphic
        #   [[shop.sell]] one line the vendor buys from a player
        #     item        the id of an item template
        #     price       gold the vendor pays for one piece, at least 1
        #     (amount, hue and name are written too and ignored on sell lines)

        """;

    // Where the name ModernUO gives a vendor class is not the one of its template.
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.Ordinal)
    {
        ["armorer"] = ["armourer", "m_armourer", "f_armourer"],
        ["waiter"] = ["waiter", "m_waiter", "f_waitress"],
        ["wanderinghealer"] = ["whealer"],
        ["evilwanderinghealer"] = ["evilwhealer"]
    };

    private static readonly Regex IdLine = new(
        "^id\\s*=\\s*\"([^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(RegexTimeoutSeconds)
    );

    private static readonly Regex Graphic = new(
        "^0x([0-9a-f]{4})_(.*)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(RegexTimeoutSeconds)
    );

    public static int Run(
        string source,
        string items,
        string mobiles,
        string destination,
        TextWriter output,
        TextWriter error
    )
    {
        var root = Directory.Exists(Path.Combine(source, MobilesFolder, VendorsFolder))
            ? Path.Combine(source, MobilesFolder, VendorsFolder)
            : source;

        if (!Directory.Exists(Path.Combine(root, "SBInfo")) || !Directory.Exists(Path.Combine(root, "NPC")))
        {
            error.WriteLine($"{source}: it must hold SBInfo and NPC folders, or Mobiles/Vendors with them.");

            return 2;
        }

        if (!Directory.Exists(items) || !Directory.Exists(mobiles))
        {
            error.WriteLine($"The item or mobile templates folder does not exist: {items}, {mobiles}");

            return 2;
        }

        try
        {
            var report = new ConversionReport();
            var sbInfos = new Dictionary<string, ImportedSbInfo>(StringComparer.Ordinal);

            foreach (var info in ReadAll(
                         Path.Combine(root, "SBInfo"),
                         (text, path) => ModernUoVendorSourceReader.ReadSbInfos(text, path, report)
                     ))
            {
                if (!sbInfos.TryAdd(info.Name, info))
                {
                    report.Count($"SBInfo {info.Name} is defined twice (the first is used)");
                }
            }

            var vendors = ReadAll(
                Path.Combine(root, "NPC"),
                (text, path) => ModernUoVendorSourceReader.ReadVendors(text, path, report)
            );
            var itemsByGraphic = ItemsByGraphic(items);
            var graphicsOfType = GraphicsOfType(sbInfos.Values);
            var mobileIds = Ids(mobiles);
            var claimed = new Dictionary<string, string>(StringComparer.Ordinal);
            var files = new List<(string Path, ShopFile Shop)>();

            foreach (var vendor in vendors.OrderBy(vendor => vendor.Name, StringComparer.Ordinal))
            {
                if (Build(
                        vendor,
                        sbInfos,
                        new(itemsByGraphic, graphicsOfType, ItemsByName(itemsByGraphic)),
                        mobileIds,
                        claimed,
                        report
                    ) is { } shop)
                {
                    files.Add((Path.Combine(destination, shop.Id + ".toml"), new() { Shop = [shop] }));
                }
            }

            CapSellPrices(files.Select(file => file.Shop.Shop[0]).ToList(), report);

            if (files.Count == 0)
            {
                ConverterOutput.WriteReport(output, report);
                error.WriteLine($"{source}: no vendor with a shop and a mobile template.");

                return 2;
            }

            foreach (var (path, shop) in files)
            {
                ConverterOutput.WriteToml(path, Header, shop);
                output.WriteLine(
                    $"shops/{Path.GetFileName(path)} ({shop.Shop[0].Buy.Count} lines, {shop.Shop[0].Vendors.Count} vendors)"
                );
            }

            ConverterOutput.WriteReport(output, report);
            output.WriteLine(
                $"Wrote {files.Count} shop(s), {files.Sum(file => file.Shop.Shop[0].Buy.Count)} lines from ModernUO's vendors."
            );

            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException
                                              or UnauthorizedAccessException or DecoderFallbackException
                                              or RegexMatchTimeoutException)
        {
            error.WriteLine($"Vendor conversion failed: {exception.Message}");

            return 2;
        }
    }

    private static ShopDefinition? Build(
        ImportedVendor vendor,
        Dictionary<string, ImportedSbInfo> sbInfos,
        ShopItemIndex index,
        HashSet<string> mobileIds,
        Dictionary<string, string> claimed,
        ConversionReport report
    )
    {
        var id = StringUtils.ToSnakeCase(vendor.Name);
        var templates = TemplatesOf(vendor.Name, mobileIds).ToList();

        if (vendor.SbInfos.Count == 0)
        {
            report.Count($"vendor {vendor.Name} adds no SBInfo");

            return null;
        }

        if (templates.Count == 0)
        {
            report.Count($"vendor {vendor.Name} has no mobile template");

            return null;
        }

        var lines = new List<ShopLine>();
        var seen = new HashSet<(string, int, int)>();

        foreach (var name in vendor.SbInfos)
        {
            if (!sbInfos.TryGetValue(name, out var info))
            {
                report.Count($"unknown SBInfo {name}");

                continue;
            }

            foreach (var line in info.Lines)
            {
                if (ItemOf(line, index.ByGraphic, report) is not { } item)
                {
                    continue;
                }

                // Two SBInfo of one vendor may sell the same thing for the same price.
                if (seen.Add((item, line.Price, line.Hue)))
                {
                    lines.Add(
                        new()
                        {
                            Item = item, Price = Math.Max(1, line.Price), Amount = Math.Max(1, line.Amount),
                            Hue = line.Hue, Name = line.Name
                        }
                    );
                }
            }
        }

        if (lines.Count == 0)
        {
            report.Count($"vendor {vendor.Name} sells nothing that has an item template");

            return null;
        }

        var vendors = new List<string>();

        foreach (var template in templates)
        {
            if (claimed.TryGetValue(template, out var other))
            {
                report.Count($"template {template} is in shop {other} already");
            }
            else
            {
                claimed[template] = id;
                vendors.Add(template);
            }
        }

        return vendors.Count == 0
            ? null
            : new() { Id = id, Vendors = vendors, Buy = lines, Sell = SellLines(vendor, sbInfos, index, report) };
    }

    // No vendor pays more for a piece than the lowest price any vendor asks for it: buying from one vendor and selling to
    // another is never a profit. ModernUO's own tables have a few such pairs.
    private static void CapSellPrices(List<ShopDefinition> shops, ConversionReport report)
    {
        var lowestBuy = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in shops.SelectMany(shop => shop.Buy))
        {
            lowestBuy[line.Item] = Math.Min(line.Price, lowestBuy.GetValueOrDefault(line.Item, int.MaxValue));
        }

        foreach (var line in shops.SelectMany(shop => shop.Sell))
        {
            if (lowestBuy.TryGetValue(line.Item, out var asked) && line.Price > asked)
            {
                report.Count($"sell price of {line.Item} lowered to {asked}, the lowest price it is sold at");
                line.Price = asked;
            }
        }
    }

    // What the vendor buys: the templates of the graphics its shops sell a type under, else the templates named like it.
    private static List<ShopLine> SellLines(
        ImportedVendor vendor,
        Dictionary<string, ImportedSbInfo> sbInfos,
        ShopItemIndex index,
        ConversionReport report
    )
    {
        var lines = new List<ShopLine>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sell in vendor.SbInfos.Where(sbInfos.ContainsKey).SelectMany(name => sbInfos[name].Sells))
        {
            // A vendor buys a piece whatever it is made of, so an armor or weapon graphic sells every template of it.
            var items = index.GraphicsOfType.GetValueOrDefault(sell.TypeName, [])
                .SelectMany(graphic =>
                    index.ByGraphic.TryGetValue(graphic, out var family) &&
                    (EraBase(family) is not null || IsMaterialFamily(family)) ? family :
                    ItemOf(
                            new() { TypeName = sell.TypeName, Price = 0, Amount = 0, Graphic = graphic, Hue = 0 },
                            index.ByGraphic,
                            report
                        )
                        is { } one ? [one] : []
                )
                .ToList();

            if (items.Count == 0)
            {
                items = index.ByName.GetValueOrDefault(StringUtils.ToSnakeCase(sell.TypeName), []);
            }

            if (items.Count == 0)
            {
                report.Count($"no item template for the sold type {sell.TypeName}");
            }

            foreach (var item in items.Where(seen.Add))
            {
                lines.Add(new() { Item = item, Price = Math.Max(1, sell.Price) });
            }
        }

        return lines;
    }

    // The graphics each C# type is sold under in any shop.
    private static Dictionary<string, HashSet<int>> GraphicsOfType(IEnumerable<ImportedSbInfo> infos)
    {
        var graphics = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        foreach (var line in infos.SelectMany(info => info.Lines))
        {
            if (!graphics.TryGetValue(line.TypeName, out var set))
            {
                graphics[line.TypeName] = set = [];
            }

            set.Add(line.Graphic);
        }

        return graphics;
    }

    // Item template ids by the words after their graphic: 0x103b_bread_loaf is bread_loaf.
    private static Dictionary<string, List<string>> ItemsByName(Dictionary<int, List<string>> itemsByGraphic)
    {
        var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var id in itemsByGraphic.Values.SelectMany(ids => ids))
        {
            var name = Graphic.Match(id).Groups[2].Value;

            if (!byName.TryGetValue(name, out var ids))
            {
                byName[name] = ids = [];
            }

            ids.Add(id);
        }

        return byName;
    }

    // The plain piece of the first era the graphic has; null for a graphic that is not an armor or weapon family.
    private static string? EraBase(List<string> candidates)
    {
        return Eras.Select(era => candidates.FirstOrDefault(candidate => Graphic.Match(candidate).Groups[2].Value == era))
            .FirstOrDefault(candidate => candidate is not null);
    }

    // Templates that are all one piece in the materials of the data sets, with no plain piece among them.
    private static bool IsMaterialFamily(List<string> candidates)
    {
        return candidates.Count > 1 &&
               candidates.All(candidate => Materials.Contains(Graphic.Match(candidate).Groups[2].Value));
    }

    // The template of a graphic: the one named like the type, else the only one, else the first (and the report says so).
    private static string? ItemOf(
        ImportedBuyLine line,
        Dictionary<int, List<string>> itemsByGraphic,
        ConversionReport report
    )
    {
        if (!itemsByGraphic.TryGetValue(line.Graphic, out var candidates))
        {
            report.Count($"no item template for graphic 0x{line.Graphic:x4} ({line.TypeName})");

            return null;
        }

        var wanted = StringUtils.ToSnakeCase(line.TypeName);
        var named = candidates.FirstOrDefault(candidate => Graphic.Match(candidate).Groups[2].Value == wanted);

        if (named is not null)
        {
            return named;
        }

        if (EraBase(candidates) is { } plain)
        {
            return plain;
        }

        if (IsMaterialFamily(candidates))
        {
            report.Count(
                $"graphic 0x{line.Graphic:x4} ({line.TypeName}) has only material templates, no plain piece: left out"
            );

            return null;
        }

        if (candidates.Count > 1)
        {
            report.Count(
                $"graphic 0x{line.Graphic:x4} ({line.TypeName}) has {candidates.Count} item templates, took {candidates[0]}"
            );
        }

        return candidates[0];
    }

    private static IEnumerable<string> TemplatesOf(string className, HashSet<string> mobileIds)
    {
        var lower = className.ToLowerInvariant();
        string[] names = Aliases.TryGetValue(lower, out var aliases) ? aliases : [lower, "m_" + lower, "f_" + lower];

        return names.Where(mobileIds.Contains);
    }

    private static Dictionary<int, List<string>> ItemsByGraphic(string folder)
    {
        var byGraphic = new Dictionary<int, List<string>>();

        foreach (var id in Ids(folder).Order(StringComparer.Ordinal))
        {
            var match = Graphic.Match(id);

            if (match.Success)
            {
                var graphic = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                if (!byGraphic.TryGetValue(graphic, out var ids))
                {
                    byGraphic[graphic] = ids = [];
                }

                ids.Add(id);
            }
        }

        return byGraphic;
    }

    // The ids of the templates of a folder: the "id = ..." lines of its files.
    private static HashSet<string> Ids(string folder)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(folder, "*.toml", SearchOption.AllDirectories))
        {
            foreach (Match match in IdLine.Matches(File.ReadAllText(path)))
            {
                ids.Add(match.Groups[1].Value);
            }
        }

        return ids;
    }

    private static List<T> ReadAll<T>(string folder, Func<string, string, IReadOnlyList<T>> read)
    {
        var all = new List<T>();

        foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            all.AddRange(read(File.ReadAllText(path, new UTF8Encoding(false, true)), path));
        }

        return all;
    }
}
