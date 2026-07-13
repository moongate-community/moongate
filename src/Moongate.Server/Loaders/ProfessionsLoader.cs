using Moongate.Server.Interfaces.Loading;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.UO.Data.Professions;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads profession presets into <see cref="IProfessionService" /> at startup: seeds the embedded
/// <c>professions.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class ProfessionsLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<ProfessionsLoader>();
    private readonly IProfessionService _professions;
    private readonly DirectoriesConfig _directories;

    public ProfessionsLoader(IProfessionService professions, DirectoriesConfig directories)
    {
        _professions = professions;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "professions.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(ProfessionsLoader).Assembly, "Assets/professions.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default professions.yaml at {Path}", path);
        }

        var definitions = YamlUtils.DeserializeFromFile<ProfessionDefinition[]>(path) ?? [];

        foreach (var definition in definitions)
        {
            _professions.Register(definition);
        }

        _logger.Information("Loaded {Count} profession(s) from {Path}", definitions.Length, path);

        return ValueTask.CompletedTask;
    }
}
