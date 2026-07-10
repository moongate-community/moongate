using Moongate.Server.Interfaces;
using Moongate.UO.Data.Items;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads item templates into <see cref="IItemTemplateService" /> at startup: seeds the embedded
/// <c>item_templates.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class ItemTemplatesLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<ItemTemplatesLoader>();
    private readonly IItemTemplateService _templates;
    private readonly DirectoriesConfig _directories;

    public ItemTemplatesLoader(IItemTemplateService templates, DirectoriesConfig directories)
    {
        _templates = templates;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "item_templates.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(ItemTemplatesLoader).Assembly, "Assets/item_templates.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default item_templates.yaml at {Path}", path);
        }

        var templates = YamlUtils.DeserializeFromFile<ItemTemplate[]>(path) ?? [];

        foreach (var template in templates)
        {
            _templates.Register(template);
        }

        _logger.Information("Loaded {Count} item template(s) from {Path}", templates.Length, path);

        return ValueTask.CompletedTask;
    }
}
