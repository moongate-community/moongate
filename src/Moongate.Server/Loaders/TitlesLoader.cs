using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Titles;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads the fame/karma title table into <see cref="ITitleService" /> at startup: seeds the embedded
/// <c>titles.yaml</c> into the data directory if missing, then parses and registers it in order.
/// </summary>
public sealed class TitlesLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<TitlesLoader>();
    private readonly ITitleService _titles;
    private readonly DirectoriesConfig _directories;

    public TitlesLoader(ITitleService titles, DirectoriesConfig directories)
    {
        _titles = titles;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "titles.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(TitlesLoader).Assembly, "Assets/titles.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default titles.yaml at {Path}", path);
        }

        var groups = YamlUtils.DeserializeFromFile<FameTitleGroup[]>(path) ?? [];

        foreach (var group in groups)
        {
            _titles.Register(group);
        }

        _logger.Information("Loaded {Count} fame title group(s) from {Path}", groups.Length, path);

        return ValueTask.CompletedTask;
    }
}
