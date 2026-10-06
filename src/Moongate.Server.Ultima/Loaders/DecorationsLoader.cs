using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Reads <c>templates/decorations/&lt;folder&gt;/*.toml</c> on demand, so an edited file is placed by the next
///     <c>.decorate</c> without a restart. A folder or a file starting with <c>_</c> is skipped; <c>britannia</c>
///     decorates Trammel
///     and Felucca, and any other folder must be named after a map.
/// </summary>
public sealed class DecorationsLoader : IDecorationsLoader
{
    private const string Britannia = "britannia";

    private readonly DirectoriesConfig _directoriesConfig;

    private string decorationsDirectory => Path.Join(_directoriesConfig["templates"], "decorations");

    public DecorationsLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task<IReadOnlyList<DecorationFile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var files = new List<DecorationFile>();

        if (!Directory.Exists(decorationsDirectory))
        {
            return files;
        }

        foreach (var folderPath in Directory.GetDirectories(decorationsDirectory).Order(StringComparer.Ordinal))
        {
            var folder = Path.GetFileName(folderPath);

            if (folder.StartsWith('_'))
            {
                continue;
            }

            var maps = MapsOf(folder, folderPath);

            foreach (var path in Directory.GetFiles(folderPath, "*.toml").Order(StringComparer.Ordinal))
            {
                // Set aside as a folder is: kept in the root, and not placed.
                if (Path.GetFileName(path).StartsWith('_'))
                {
                    continue;
                }

                var document = TomlSerializer.Deserialize<TomlTable>(await File.ReadAllTextAsync(path, cancellationToken))!;
                var blocks = document.TryGetValue("decoration", out var array) && array is TomlTableArray tables
                    ? tables.Select(table => ReadBlock(path, table)).ToList()
                    : [];

                files.Add(
                    new()
                    {
                        Folder = folder,
                        Name = Path.GetFileNameWithoutExtension(path),
                        Maps = maps,
                        Blocks = blocks
                    }
                );
            }
        }

        return files;
    }

    private static IReadOnlyList<MapType> MapsOf(string folder, string folderPath)
    {
        if (folder == Britannia)
        {
            return [MapType.Trammel, MapType.Felucca];
        }

        // Lower case only: the folders are named like the rest of the templates.
        if (folder.All(char.IsLower) && Enum.TryParse<MapType>(folder, true, out var map))
        {
            return [map];
        }

        throw new InvalidDataException(
            $"{folderPath}: decoration folder '{folder}' is not a map or britannia; start its name with _ to skip it."
        );
    }

    private static DecorationBlock ReadBlock(string path, TomlTable table)
    {
        if (!table.TryGetValue("type", out var type) || type is not string typeName || string.IsNullOrWhiteSpace(typeName))
        {
            throw new InvalidDataException($"{path}: a decoration block has no type.");
        }

        var props = new Dictionary<string, object>(StringComparer.Ordinal);

        if (table.TryGetValue("props", out var rawProps) && rawProps is TomlTable propTable)
        {
            foreach (var (key, value) in propTable)
            {
                if (value is string or long or double or bool)
                {
                    props[key] = value;
                }
                else if (value is TomlArray { Count: 3 } xyz && xyz.All(part => part is long))
                {
                    // A point, such as a teleporter's point_dest.
                    props[key] = new Point3D((int)(long)xyz[0]!, (int)(long)xyz[1]!, (int)(long)xyz[2]!);
                }
            }
        }

        var locations = new List<Point3D>();

        if (table.TryGetValue("locations", out var rawLocations) && rawLocations is TomlArray array)
        {
            foreach (var location in array)
            {
                if (location is not TomlArray { Count: 3 } xyz || xyz.Any(value => value is not long))
                {
                    throw new InvalidDataException($"{path}: a location of '{typeName}' is not [x, y, z].");
                }

                locations.Add(new((int)(long)xyz[0]!, (int)(long)xyz[1]!, (int)(long)xyz[2]!));
            }
        }

        return new()
        {
            Type = typeName,
            Comment = table.TryGetValue("comment", out var comment) ? comment as string : null,
            ItemId = table.TryGetValue("item_id", out var itemId) && itemId is long graphic ? (int)graphic : null,
            Props = props,
            Locations = locations
        };
    }
}
