using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts ModernUO's world and dungeon teleporters ( <c>Distribution/Data/teleporters.json</c>, placed there by
///     <c>[TelGen</c>) into decoration files: <c>&lt;map&gt;/teleporters.toml</c> for each map with teleporters,
///     replacing those of a previous run. An entry is a source, a destination and <c>back</c>, which adds the
///     teleporter
///     from the destination to the source.
/// </summary>
internal static class ModernUoTeleporterConverter
{
    private const string FileName = "teleporters.toml";
    private const int TeleporterGraphic = 0x1BC3;

    // [TelGen deletes the teleporters this close in height to a new one on the same cell.
    private const int SameSpotHeight = 12;

    // ModernUO's map names and the decoration folder of each.
    private static readonly (string Map, string Folder)[] Maps =
    [
        ("Felucca", "felucca"), ("Trammel", "trammel"), ("Ilshenar", "ilshenar"), ("Malas", "malas"), ("Tokuno", "tokuno"),
        ("TerMur", "termur")
    ];

    public static int Run(string source, string destination, TextWriter output, TextWriter error)
    {
        if (!File.Exists(source))
        {
            error.WriteLine($"ModernUO teleporters.json does not exist: {source}");

            return 2;
        }

        // Per map, in the order the teleporters are made.
        var teleporters = Maps.Select(_ => new List<(int X, int Y, int Z, int Map, int DestX, int DestY, int DestZ)>())
            .ToArray();

        try
        {
            // As ModernUO's own reader: comments and trailing commas are fine.
            using var document = JsonDocument.Parse(
                File.ReadAllText(source),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            );
            var index = 0;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                index++;

                JsonElement back = default;
                var hasBack = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("back", out back);

                if (!TryReadPlace(entry, "src", out var from) ||
                    !TryReadPlace(entry, "dst", out var to) ||
                    hasBack && back.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    error.WriteLine($"{source}: entry {index} is not a teleporter: {entry.GetRawText()}");

                    return 2;
                }

                Add(teleporters[from.Map], from, to);

                if (hasBack && back.ValueKind == JsonValueKind.True)
                {
                    Add(teleporters[to.Map], to, from);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            error.WriteLine($"{source}: not valid JSON, or not a list of teleporters: {exception.Message}");

            return 2;
        }

        // An empty list would only delete the files of an earlier run.
        if (teleporters.All(list => list.Count == 0))
        {
            error.WriteLine($"{source}: no teleporters in it.");

            return 2;
        }

        for (var map = 0; map < Maps.Length; map++)
        {
            var path = Path.Combine(destination, Maps[map].Folder, FileName);

            if (teleporters[map].Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                continue;
            }

            // One block per destination, in the order of its first teleporter.
            var blocks = teleporters[map]
                .GroupBy(teleporter => (teleporter.Map, teleporter.DestX, teleporter.DestY, teleporter.DestZ))
                .ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Write(map, blocks));
            output.WriteLine(
                $"{Maps[map].Folder}/{FileName}: {teleporters[map].Count} teleporters in {blocks.Count} blocks"
            );
        }

        return 0;
    }

    private static void Add(
        List<(int X, int Y, int Z, int Map, int DestX, int DestY, int DestZ)> teleporters,
        (int Map, int X, int Y, int Z) from,
        (int Map, int X, int Y, int Z) to
    )
    {
        teleporters.RemoveAll(other => other.X == from.X && other.Y == from.Y && Math.Abs(other.Z - from.Z) <= SameSpotHeight
        );
        teleporters.Add((from.X, from.Y, from.Z, to.Map, to.X, to.Y, to.Z));
    }

    private static bool TryReadPlace(JsonElement entry, string name, out (int Map, int X, int Y, int Z) place)
    {
        place = default;

        if (entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("map", out var mapName) ||
            mapName.ValueKind != JsonValueKind.String ||
            !value.TryGetProperty("loc", out var location) ||
            location.ValueKind != JsonValueKind.Array ||
            location.GetArrayLength() != 3)
        {
            return false;
        }

        var map = Array.FindIndex(Maps, known => known.Map == mapName.GetString());

        var x = 0;
        var y = 0;
        var z = 0;

        if (map < 0 ||
            location.EnumerateArray().Any(part => part.ValueKind != JsonValueKind.Number) ||
            !location[0].TryGetInt32(out x) ||
            !location[1].TryGetInt32(out y) ||
            !location[2].TryGetInt32(out z))
        {
            return false;
        }

        place = (map, x, y, z);

        return true;
    }

    private static string Write(
        int map,
        List<IGrouping<(int Map, int DestX, int DestY, int DestZ), (int X, int Y, int Z, int Map, int DestX, int DestY, int
            DestZ)>> blocks
    )
    {
        var text = new StringBuilder();
        text.Append(
            CultureInfo.InvariantCulture,
            $"""
             # ==============================================================================
             # Moongate - templates/decorations/{Maps[map].Folder}/{FileName}
             #
             # What it is for:
             #   The world and dungeon teleporters placed on {Maps[map].Map}. Each block is one
             #   destination, with a teleporter at every location it lists.
             #
             # Fields:
             #   type       Teleporter
             #   item_id    the graphic, seen by staff only
             #   props      point_dest, [x, y, z] of the destination; map_dest, its map when
             #              it is another one
             #   locations  [x, y, z] of every teleporter of the block
             # ==============================================================================

             """
        );

        foreach (var block in blocks)
        {
            var props = string.Create(
                CultureInfo.InvariantCulture,
                $"point_dest = [{block.Key.DestX}, {block.Key.DestY}, {block.Key.DestZ}]"
            );

            if (block.Key.Map != map)
            {
                props += $", map_dest = \"{Maps[block.Key.Map].Map}\"";
            }

            text.AppendLine();
            text.AppendLine("[[decoration]]");
            text.AppendLine("type = \"Teleporter\"");
            text.AppendLine(CultureInfo.InvariantCulture, $"item_id = 0x{TeleporterGraphic:X4}");
            text.AppendLine(CultureInfo.InvariantCulture, $"props = {{ {props} }}");

            var locations = block.ToList();

            if (locations.Count == 1)
            {
                text.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"locations = [[{locations[0].X}, {locations[0].Y}, {locations[0].Z}]]"
                );

                continue;
            }

            text.AppendLine("locations = [");

            foreach (var location in locations)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"    [{location.X}, {location.Y}, {location.Z}],");
            }

            text.AppendLine("]");
        }

        return text.ToString();
    }
}
