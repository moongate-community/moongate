using System.Globalization;
using System.Text;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts ModernUO's shop and world signs ( <c>Distribution/Data/signs.cfg</c>, placed there by <c>[SignGen</c>)
///     into decoration files: <c>&lt;folder&gt;/signs.toml</c> for <c>britannia</c> (Trammel and Felucca) and for each
///     map with signs of its own, replacing those of a previous run. A line is
///     <c>&lt;facet&gt; &lt;graphic&gt; &lt;x&gt; &lt;y&gt; &lt;z&gt; &lt;text&gt;</c>; a text of <c>#</c> and a number
///     is a cliloc.
/// </summary>
internal static class ModernUoSignConverter
{
    private const string FileName = "signs.toml";

    // signs.cfg's facet numbers, in order: 0 is Trammel and Felucca.
    private static readonly string[] Folders = ["britannia", "felucca", "trammel", "ilshenar", "malas", "tokuno"];

    private static readonly string[] MapNames =
        ["Trammel and Felucca", "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno"];

    public static int Run(string source, string destination, TextWriter output, TextWriter error)
    {
        if (!File.Exists(source))
        {
            error.WriteLine($"ModernUO signs.cfg does not exist: {source}");

            return 2;
        }

        // Per facet: the blocks in the order of their first sign, each with its locations.
        var blocks = Folders
            .Select(_ => new List<(int ItemId, string Text, int Hue, List<(int X, int Y, int Z)> Locations)>())
            .ToArray();
        var lines = File.ReadAllLines(source);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ', 6);

            if (parts.Length != 6 ||
                !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var facet) ||
                facet < 0 ||
                facet >= Folders.Length ||
                !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var itemId) ||
                !int.TryParse(parts[2], CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[3], CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[4], CultureInfo.InvariantCulture, out var z) ||
                parts[5].Length == 0 ||
                (parts[5][0] == '#' && !int.TryParse(parts[5].AsSpan(1), CultureInfo.InvariantCulture, out _)))
            {
                error.WriteLine($"{source}: line {index + 1} is not a sign: {line}");

                return 2;
            }

            var hue = Hue(facet, x, y);
            var facetBlocks = blocks[facet];
            var at = facetBlocks.FindIndex(block => block.ItemId == itemId && block.Hue == hue && block.Text == parts[5]);

            if (at < 0)
            {
                facetBlocks.Add((itemId, parts[5], hue, []));
                at = facetBlocks.Count - 1;
            }

            facetBlocks[at].Locations.Add((x, y, z));
        }

        for (var facet = 0; facet < Folders.Length; facet++)
        {
            var path = Path.Combine(destination, Folders[facet], FileNameOf(facet));

            if (blocks[facet].Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Write(facet, blocks[facet]));
            output.WriteLine(
                $"{Folders[facet]}/{FileNameOf(facet)}: {blocks[facet].Sum(block => block.Locations.Count)} signs in {blocks[facet].Count} blocks"
            );
        }

        return 0;
    }

    // The signs of Trammel alone are those of the old Haven, which is a ruin on the map of a modern client: their
    // file is set aside with an underscore, as the other decoration of that town, and the loader skips it.
    private static string FileNameOf(int facet)
    {
        return Folders[facet] == "trammel" ? "_" + FileName : FileName;
    }

    // ModernUO's SignParser: the signs of Luna and Umbra take the hue of their town.
    private static int Hue(int facet, int x, int y)
    {
        if (Folders[facet] != "malas")
        {
            return 0;
        }

        return x switch
        {
            >= 965 when y >= 502 && x <= 1012 && y <= 537  => 0x47E,
            >= 1960 when y >= 1278 && x < 2106 && y < 1413 => 0x44E,
            _                                              => 0
        };
    }

    private static string Write(
        int facet,
        List<(int ItemId, string Text, int Hue, List<(int X, int Y, int Z)> Locations)> blocks
    )
    {
        var text = new StringBuilder();
        text.Append(
            CultureInfo.InvariantCulture,
            $"""
             # ==============================================================================
             # Moongate - templates/decorations/{Folders[facet]}/{FileNameOf(facet)}
             #
             # What it is for:
             #   The shop and world signs placed on {MapNames[facet]}. Each block is one sign
             #   placed at every location it lists.
             #
             # Fields:
             #   type       LocalizedSign for a text of the client (label_number), Sign for a
             #              written name
             #   item_id    the graphic
             #   props      label_number, the cliloc of the text, or name; hue when the sign
             #              has one
             #   locations  [x, y, z] of every sign of the block
             # ==============================================================================

             """
        );

        foreach (var block in blocks)
        {
            var localized = block.Text[0] == '#';
            var props = localized
                ? $"label_number = {block.Text[1..]}"
                : $"name = \"{block.Text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

            if (block.Hue != 0)
            {
                props += string.Create(CultureInfo.InvariantCulture, $", hue = 0x{block.Hue:X4}");
            }

            text.AppendLine();
            text.AppendLine("[[decoration]]");
            text.AppendLine(localized ? "type = \"LocalizedSign\"" : "type = \"Sign\"");
            text.AppendLine(CultureInfo.InvariantCulture, $"item_id = 0x{block.ItemId:X4}");
            text.AppendLine(CultureInfo.InvariantCulture, $"props = {{ {props} }}");

            if (block.Locations.Count == 1)
            {
                var (x, y, z) = block.Locations[0];
                text.AppendLine(CultureInfo.InvariantCulture, $"locations = [[{x}, {y}, {z}]]");

                continue;
            }

            text.AppendLine("locations = [");

            foreach (var (x, y, z) in block.Locations)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"    [{x}, {y}, {z}],");
            }

            text.AppendLine("]");
        }

        return text.ToString();
    }
}
