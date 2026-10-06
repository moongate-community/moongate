using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts the named places of ModernUO's <c>[Go</c> gump ( <c>Distribution/Data/Locations/&lt;map&gt;.json</c>,
///     nested categories of places) into one data file, <c>locations.toml</c>: a <c>[[location]]</c> per place with its
///     map, its categories joined by <c>/</c>, its name and its spot.
/// </summary>
internal static class ModernUoLocationConverter
{
    private const string CategorySeparator = "/";

    // The map files, in the order the places are written.
    private static readonly string[] Maps = ["felucca", "trammel", "ilshenar", "malas", "tokuno", "termur"];

    public static int Run(string source, string destination, TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(source))
        {
            error.WriteLine($"ModernUO Locations folder does not exist: {source}");

            return 2;
        }

        var places = new List<(string Map, string Category, string Name, int X, int Y, int Z)>();
        var maps = 0;

        foreach (var map in Maps)
        {
            var path = Path.Combine(source, map + ".json");

            if (!File.Exists(path))
            {
                continue;
            }

            maps++;

            try
            {
                // As ModernUO's own reader: comments and trailing commas are fine.
                using var document = JsonDocument.Parse(
                    File.ReadAllText(path),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
                );

                if (Read(document.RootElement, map, "", places) is { } problem)
                {
                    error.WriteLine($"{path}: {problem}");

                    return 2;
                }
            }
            catch (JsonException exception)
            {
                error.WriteLine($"{path}: not valid JSON: {exception.Message}");

                return 2;
            }
        }

        if (places.Count == 0)
        {
            error.WriteLine($"{source}: no places in it; it must hold files such as felucca.json.");

            return 2;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, Write(places));
        output.WriteLine($"{Path.GetFileName(destination)}: {places.Count} places on {maps} maps");

        return 0;
    }

    // Reads the places of a category and of those inside it; a problem as text, or null.
    private static string? Read(
        JsonElement node,
        string map,
        string category,
        List<(string Map, string Category, string Name, int X, int Y, int Z)> places
    )
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return $"a category of '{category}' is not an object.";
        }

        if (node.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array)
        {
            foreach (var place in locations.EnumerateArray())
            {
                if (place.ValueKind != JsonValueKind.Object ||
                    !place.TryGetProperty("name", out var name) ||
                    name.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(name.GetString()))
                {
                    return $"a place of '{category}' has no name.";
                }

                if (!place.TryGetProperty("location", out var spot) ||
                    spot.ValueKind != JsonValueKind.Array ||
                    spot.GetArrayLength() != 3 ||
                    spot.EnumerateArray().Any(part => part.ValueKind != JsonValueKind.Number) ||
                    !spot[0].TryGetInt32(out var x) ||
                    !spot[1].TryGetInt32(out var y) ||
                    !spot[2].TryGetInt32(out var z))
                {
                    return $"the place '{name.GetString()}' has no location [x, y, z].";
                }

                places.Add((map, category, name.GetString()!.Trim(), x, y, z));
            }
        }

        if (!node.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var child in categories.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object ||
                !child.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(name.GetString()))
            {
                return $"a category of '{category}' has no name.";
            }

            // The separator inside a name would split the category in two.
            var segment = name.GetString()!.Trim().Replace(CategorySeparator, "-", StringComparison.Ordinal);
            var path = category.Length == 0 ? segment : category + CategorySeparator + segment;

            if (Read(child, map, path, places) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    private static string Write(List<(string Map, string Category, string Name, int X, int Y, int Z)> places)
    {
        var text = new StringBuilder();
        text.Append(
            """
            # ==============================================================================
            # Moongate - locations.toml
            #
            # What it is for:
            #   The named places staff travels to: ".go" opens a gump that lists them by
            #   map and category, and ".go <name>" goes to one. A place of a map the
            #   server does not load is left out.
            #
            # Fields:
            #   [[location]]  one per place, in the order the gump lists them
            #   map           felucca, trammel, ilshenar, malas, tokuno or termur
            #   category      where the gump files it: the categories from the map down,
            #                 joined by "/", such as "Dungeons/Covetous"; empty for a
            #                 place listed under the map itself
            #   name          the name shown, such as "Entrance"
            #   location      where the traveller arrives, "(x, y, z)"
            # ==============================================================================

            """
        );

        foreach (var place in places)
        {
            text.AppendLine();
            text.AppendLine("[[location]]");
            text.AppendLine(CultureInfo.InvariantCulture, $"map = \"{place.Map}\"");
            text.AppendLine(CultureInfo.InvariantCulture, $"category = \"{Escaped(place.Category)}\"");
            text.AppendLine(CultureInfo.InvariantCulture, $"name = \"{Escaped(place.Name)}\"");
            text.AppendLine(CultureInfo.InvariantCulture, $"location = \"({place.X}, {place.Y}, {place.Z})\"");
        }

        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Escaped(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
