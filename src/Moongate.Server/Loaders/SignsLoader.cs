using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Signs;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads world sign placements into <see cref="ISignService" /> at startup: seeds the embedded
/// <c>signs.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class SignsLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<SignsLoader>();
    private readonly ISignService _signs;
    private readonly DirectoriesConfig _directories;

    public SignsLoader(ISignService signs, DirectoriesConfig directories)
    {
        _signs = signs;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "signs.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(SignsLoader).Assembly, "Assets/signs.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default signs.yaml at {Path}", path);
        }

        var signs = YamlUtils.DeserializeFromFile<SignEntry[]>(path) ?? [];

        foreach (var sign in signs)
        {
            _signs.Register(sign);
        }

        _logger.Information("Loaded {Count} sign(s) from {Path}", signs.Length, path);

        return ValueTask.CompletedTask;
    }
}
