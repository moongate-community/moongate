using Moongate.Server.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Internal;
using Moongate.Server.Services.Items;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Server.Loaders;

public sealed class LootTemplatesLoader : IDataLoader
{
    private const string EmbeddedDirectory = "Assets/Templates/Loot";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Templates.Loot";

    private readonly ILogger _logger = Log.ForContext<LootTemplatesLoader>();
    private readonly ILootTemplateService _lootTemplates;
    private readonly IItemTemplateService _itemTemplates;
    private readonly DirectoriesConfig _directories;

    public LootTemplatesLoader(
        ILootTemplateService lootTemplates,
        IItemTemplateService itemTemplates,
        DirectoriesConfig directories
    )
    {
        _lootTemplates = lootTemplates;
        _itemTemplates = itemTemplates;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var templatesDirectory = _directories.RegisterDirectory("templates");
        var lootDirectory = Path.Combine(templatesDirectory, "loot");
        var directoryExists = Directory.Exists(lootDirectory);

        if (!directoryExists)
        {
            EmbeddedResourceDirectorySeeder.SeedAtomic(
                typeof(LootTemplatesLoader).Assembly,
                EmbeddedDirectory,
                EmbeddedNamespace,
                lootDirectory
            );
        }

        var files = Directory.GetFiles(lootDirectory, "*.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            if (directoryExists)
            {
                _logger.Warning("No loot template YAML files found in existing directory {Path}", lootDirectory);
            }

            return ValueTask.CompletedTask;
        }

        var sources = new List<LootTemplateSource>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(lootDirectory, file);
            var templates = LootTemplateYamlDeserializer.DeserializeFromFile(file, relativePath);

            sources.AddRange(templates.Select(template => new LootTemplateSource(relativePath, template)));
        }

        LootTemplateValidator.Validate(sources, _itemTemplates.All);

        foreach (var source in sources)
        {
            _lootTemplates.Register(source.Template);
        }

        _logger.Information(
            "Loaded {TemplateCount} loot template(s) from {YamlFileCount} YAML file(s) in {Path}",
            sources.Count,
            files.Length,
            lootDirectory
        );

        return ValueTask.CompletedTask;
    }
}
