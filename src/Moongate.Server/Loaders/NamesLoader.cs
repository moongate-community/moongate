using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Names;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads name pools into <see cref="INameService" /> at startup: seeds the embedded <c>names.yaml</c>
/// into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class NamesLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<NamesLoader>();
    private readonly INameService _names;
    private readonly DirectoriesConfig _directories;

    public NamesLoader(INameService names, DirectoriesConfig directories)
    {
        _names = names;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "names.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(NamesLoader).Assembly, "Assets/names.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default names.yaml at {Path}", path);
        }

        var lists = YamlUtils.DeserializeFromFile<NameList[]>(path) ?? [];

        foreach (var list in lists)
        {
            _names.Register(list);
        }

        _logger.Information("Loaded {Count} name list(s) from {Path}", lists.Length, path);

        return ValueTask.CompletedTask;
    }
}
