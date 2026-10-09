using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Utils;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the resources of <c>data/harvest.toml</c>. The file may be missing: nothing is gathered then. A resource
///     with a bad id or an id used twice, an area outside 1 to 256, an amount below 1 or a least above the most, or a
///     negative or inverted time, or one above a week, stops the server at startup.
/// </summary>
public class HarvestLoader : IDataLoader<HarvestResource>
{
    private const int MaxArea = 256;

    // A week: more is a resource that never comes back, and a number the service could not add up.
    private const int MaxRespawnMinutes = 10_080;

    private readonly ILogger _logger = Log.ForContext<HarvestLoader>();

    private readonly DirectoriesConfig _directoriesConfig;

    private string harvestFilePath => Path.Join(_directoriesConfig["data"], "harvest.toml");

    public HarvestLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<HarvestResource>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(harvestFilePath))
        {
            _logger.Information("No harvest.toml: nothing is gathered from the world");

            return new DataLoaderResult<HarvestResource> { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<HarvestFile>(harvestFilePath, null, cancellationToken) ??
                   new HarvestFile();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in file.Resource)
        {
            if (!ScriptIdUtils.IsValid(resource.Id))
            {
                throw Invalid($"the resource id '{resource.Id}' {ScriptIdUtils.Rule}");
            }

            if (!ids.Add(resource.Id))
            {
                throw Invalid($"the resource {resource.Id} is there twice");
            }

            if (resource.Area is < 1 or > MaxArea)
            {
                throw Invalid($"the area of {resource.Id} must be 1 to {MaxArea} tiles");
            }

            if (resource.AmountMin < 1 || resource.AmountMax < resource.AmountMin)
            {
                throw Invalid($"the amounts of {resource.Id} must be at least 1, the least not above the most");
            }

            // A resource that names the least time only comes back after exactly that.
            if (resource.RespawnMaxMinutes == 0)
            {
                resource.RespawnMaxMinutes = resource.RespawnMinMinutes;
            }

            if (resource.RespawnMinMinutes < 0 ||
                resource.RespawnMaxMinutes < resource.RespawnMinMinutes ||
                resource.RespawnMaxMinutes > MaxRespawnMinutes)
            {
                throw Invalid(
                    $"the respawn minutes of {resource.Id} must be 0 to {MaxRespawnMinutes}, the least not above the most"
                );
            }
        }

        if (file.Resource.Count == 0)
        {
            _logger.Warning("{Path} has no [[resource]]: nothing is gathered from the world", harvestFilePath);
        }

        _logger.Information("Found {Count} harvest resources", file.Resource.Count);

        return new DataLoaderResult<HarvestResource> { Entities = file.Resource };
    }

    private InvalidDataException Invalid(string reason)
    {
        return new($"{harvestFilePath}: {reason}.");
    }
}
