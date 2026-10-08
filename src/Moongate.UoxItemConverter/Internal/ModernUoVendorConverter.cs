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
            var sbInfos = ReadAll(
                    Path.Combine(root, "SBInfo"),
                    (text, path) => ModernUoVendorSourceReader.ReadSbInfos(text, path, report)
                )
                .GroupBy(info => info.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var vendors = ReadAll(
                Path.Combine(root, "NPC"),
                (text, path) => ModernUoVendorSourceReader.ReadVendors(text, path, report)
            );
            var itemsByGraphic = ItemsByGraphic(items);
            var mobileIds = Ids(mobiles);
            var claimed = new Dictionary<string, string>(StringComparer.Ordinal);
            var files = new List<(string Path, ShopFile Shop)>();

            foreach (var vendor in vendors.OrderBy(vendor => vendor.Name, StringComparer.Ordinal))
            {
                if (Build(vendor, sbInfos, itemsByGraphic, mobileIds, claimed, report) is { } shop)
                {
                    files.Add((Path.Combine(destination, shop.Id + ".toml"), new() { Shop = [shop] }));
                }
            }

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
        Dictionary<int, List<string>> itemsByGraphic,
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
                if (ItemOf(line, itemsByGraphic, report) is not { } item)
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

        return vendors.Count == 0 ? null : new() { Id = id, Vendors = vendors, Buy = lines };
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
