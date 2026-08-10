using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.World;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads the world's decoration into <see cref="IDecorationCatalog" /> at startup: seeds each embedded
/// <c>World/Decorations/&lt;facet&gt;.yaml</c> into the data directory if missing, then parses it.
/// <para>
/// Loading is not placing. This fills the catalogue; the objects only become world when someone runs
/// the decorate command, because 40,291 persisted items is not something a boot should do behind your
/// back.
/// </para>
/// </summary>
public sealed class DecorationsLoader : IDataLoader
{
    /// <summary>
    /// The facet files, and the map id each carries. Britannia and the two Magincia ruins are named
    /// for their content rather than a facet: Britannia is Felucca's landmass, and the ruins are the
    /// post-invasion versions of the Magincia on each of the two mirror facets.
    /// </summary>
    private static readonly (string File, int MapId)[] Facets =
    [
        ("britannia", 0),
        ("felucca", 0),
        ("trammel", 1),
        ("ilshenar", 2),
        ("malas", 3),
        ("tokuno", 4),
        ("ruinedmaginciafel", 0),
        ("ruinedmaginciatram", 1)
    ];

    private readonly ILogger _logger = Log.ForContext<DecorationsLoader>();
    private readonly IDecorationCatalog _decorations;
    private readonly DirectoriesConfig _directories;

    public DecorationsLoader(IDecorationCatalog decorations, DirectoriesConfig directories)
    {
        _decorations = decorations;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var directory = Path.Combine(_directories.RegisterDirectory("data"), "decorations");
        Directory.CreateDirectory(directory);

        foreach (var (file, mapId) in Facets)
        {
            var path = Path.Combine(directory, file + ".yaml");

            if (!File.Exists(path))
            {
                var seed = ResourceUtils.GetEmbeddedResourceString(
                    typeof(DecorationsLoader).Assembly,
                    $"Assets/World/Decorations/{file}.yaml"
                );
                File.WriteAllText(path, seed);
                _logger.Information("Seeded default {File}.yaml at {Path}", file, path);
            }

            var groups = YamlUtils.DeserializeFromFile<List<DecorationGroup>>(path);
            _decorations.Add(mapId, groups ?? []);
        }

        _logger.Information(
            "Loaded {Count} decoration object(s) across {Facets} facet file(s)",
            _decorations.All.Count,
            Facets.Length
        );

        return ValueTask.CompletedTask;
    }
}
