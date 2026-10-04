using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the jail of <c>data/jail.toml</c>. The file may be missing: there is then no jail. A file without a map,
///     without a cell, with a cell number below 1 or used twice, or with a release spot or a cell location that is
///     missing or outside the 16-bit coordinates stops the server at startup.
/// </summary>
public class JailLoader : IDataLoader<JailFile>
{
    private readonly ILogger _logger = Log.ForContext<JailLoader>();

    private readonly DirectoriesConfig _directoriesConfig;

    private string jailFilePath => Path.Join(_directoriesConfig["data"], "jail.toml");

    public JailLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<JailFile>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jailFilePath))
        {
            _logger.Information("No jail.toml: the jail is off");

            return new DataLoaderResult<JailFile> { Entities = [] };
        }

        var jail = await TomlUtils.DeserializeFromFileAsync<JailFile>(jailFilePath, null, cancellationToken) ?? new JailFile();

        if (!Enum.IsDefined(jail.Map))
        {
            throw new InvalidDataException($"{jailFilePath}: the jail has no map.");
        }

        if (!IsOnAMap(jail.Release))
        {
            throw new InvalidDataException($"{jailFilePath}: release must be a location inside the map coordinates.");
        }

        if (jail.Cell.Count == 0)
        {
            throw new InvalidDataException($"{jailFilePath}: the jail has no [[cell]].");
        }

        var numbers = new HashSet<int>();

        foreach (var cell in jail.Cell)
        {
            if (cell.Number < 1 || !numbers.Add(cell.Number))
            {
                throw new InvalidDataException(
                    $"{jailFilePath}: the cell number {cell.Number} is below 1 or used twice."
                );
            }

            if (!IsOnAMap(cell.Location))
            {
                throw new InvalidDataException(
                    $"{jailFilePath}: cell {cell.Number} needs a location inside the map coordinates."
                );
            }
        }

        _logger.Information("Found {Count} jail cells on {Map}", jail.Cell.Count, jail.Map);

        return new DataLoaderResult<JailFile> { Entities = [jail] };
    }

    // Nothing stands on the corner of the map: a location of zeros is one left out.
    private static bool IsOnAMap(Point3D location)
    {
        return location != default &&
               location.X is >= 0 and <= ushort.MaxValue &&
               location.Y is >= 0 and <= ushort.MaxValue &&
               location.Z is >= sbyte.MinValue and <= sbyte.MaxValue;
    }
}
