using System.Globalization;
using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads <c>data/bodies.toml</c> as one <see cref="BodyContent" /> per body id, sorted by id. A body listed under
///     two kinds, a range whose start is past its end or an id above 0xFFFF stops the server at startup.
/// </summary>
public class BodiesLoader : IDataLoader<BodyContent>
{
    private const int MaxBodyId = 0xFFFF;

    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<BodiesLoader>();

    private string bodiesFilePath => Path.Join(_directoriesConfig["data"], "bodies.toml");

    public BodiesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(bodiesFilePath))
        {
            throw new FileNotFoundException("Bodies file bodies.toml not found", bodiesFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<BodyContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var bodiesFile = await TomlUtils.DeserializeFromFileAsync<BodyContentFile>(bodiesFilePath, null, cancellationToken) ??
                         new BodyContentFile();
        var types = new SortedDictionary<int, BodyType>();

        Add(types, bodiesFile.Human, BodyType.Human);
        Add(types, bodiesFile.Animal, BodyType.Animal);
        Add(types, bodiesFile.Monster, BodyType.Monster);
        Add(types, bodiesFile.Sea, BodyType.Sea);
        Add(types, bodiesFile.Equipment, BodyType.Equipment);

        _logger.Information("Found {Count} bodies", types.Count);

        return new DataLoaderResult<BodyContent>()
        {
            Entities = types.Select(pair => new BodyContent { Body = new((ushort)pair.Key), Type = pair.Value }).ToList()
        };
    }

    private void Add(SortedDictionary<int, BodyType> types, List<string> entries, BodyType type)
    {
        foreach (var entry in entries)
        {
            var (first, last) = ParseRange(entry);

            for (var id = first; id <= last; id++)
            {
                if (!types.TryAdd(id, type))
                {
                    throw new InvalidDataException(
                        $"{bodiesFilePath}: body {id} is listed as both {types[id]} and {type}."
                    );
                }
            }
        }
    }

    private (int First, int Last) ParseRange(string entry)
    {
        var parts = entry.Split('-');
        var first = 0;
        var last = 0;
        var valid = parts.Length is 1 or 2 &&
                    int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out first) &&
                    int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out last) &&
                    first <= last &&
                    last <= MaxBodyId;

        if (!valid)
        {
            throw new InvalidDataException(
                $"{bodiesFilePath}: '{entry}' is not a body id or a \"min-max\" range of ids from 0 to {MaxBodyId}."
            );
        }

        return (first, last);
    }
}
