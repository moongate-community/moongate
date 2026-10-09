using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the creatures of <c>data/taming.toml</c>. The file may be missing: nothing can be tamed then. A creature
///     whose template is not a mobile template or is there twice, a minimum skill outside 0 to 120 or slots outside 1 to
///     10 stop the server at startup, naming the creature.
/// </summary>
public class TamingLoader : IDataLoader<TamingCreature>
{
    private const double MaxSkill = 120;
    private const int MaxSlots = 10;

    private readonly ILogger _logger = Log.ForContext<TamingLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private string tamingFilePath => Path.Join(_directoriesConfig["data"], "taming.toml");

    public TamingLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<TamingCreature>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(tamingFilePath))
        {
            _logger.Information("No taming.toml: no creature can be tamed");

            return new DataLoaderResult<TamingCreature> { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<TamingFile>(tamingFilePath, null, cancellationToken) ??
                   new TamingFile();
        var mobileIds = _dataLoaderService.GetEntities<MobileTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var creature in file.Creature)
        {
            if (!mobileIds.Contains(creature.Template))
            {
                throw Invalid($"the creature '{creature.Template}' is not a mobile template");
            }

            if (!seen.Add(creature.Template))
            {
                throw Invalid($"the creature {creature.Template} is there twice");
            }

            if (creature.MinSkill is < 0 or > MaxSkill || double.IsNaN(creature.MinSkill))
            {
                throw Invalid($"the minimum skill of {creature.Template} must be 0 to {MaxSkill}");
            }

            if (creature.Slots is < 1 or > MaxSlots)
            {
                throw Invalid($"the slots of {creature.Template} must be 1 to {MaxSlots}");
            }
        }

        _logger.Information("Found {Count} tamable creatures", file.Creature.Count);

        return new DataLoaderResult<TamingCreature> { Entities = file.Creature };
    }

    private InvalidDataException Invalid(string reason)
    {
        return new($"{tamingFilePath}: {reason}.");
    }
}
