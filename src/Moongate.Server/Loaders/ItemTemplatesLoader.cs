using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Data.Internal;
using Moongate.Server.Internal;
using Moongate.Server.Services.Items;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Server.Loaders;

/// <summary>Seeds and loads item templates from the recursive item template directory.</summary>
public sealed class ItemTemplatesLoader : IDataLoader
{
    private const string EmbeddedDirectory = "Assets/Templates/Items";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Templates.Items";

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
        var legacyFile = Path.Combine(dataDirectory, "item_templates.yaml");
        var templatesDirectory = _directories.RegisterDirectory("templates");
        var itemsDirectory = Path.Combine(templatesDirectory, "items");
        var directoryExists = Directory.Exists(itemsDirectory);

        if (File.Exists(legacyFile))
        {
            _logger.Warning(
                "Ignoring obsolete monolithic item template file {LegacyPath}; item templates now ship as split " +
                "YAML under {TargetPath}. Convert it offline into the embedded assets if it still holds custom items",
                legacyFile,
                itemsDirectory
            );
        }

        if (!directoryExists)
        {
            EmbeddedResourceDirectorySeeder.SeedAtomic(
                typeof(ItemTemplatesLoader).Assembly,
                EmbeddedDirectory,
                EmbeddedNamespace,
                itemsDirectory
            );
        }

        var files = Directory.GetFiles(itemsDirectory, "*.yaml", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

        if (files.Length == 0)
        {
            if (directoryExists)
            {
                _logger.Warning("No item template YAML files found in existing directory {Path}", itemsDirectory);
            }

            return ValueTask.CompletedTask;
        }

        var sources = new List<ItemTemplateSource>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(itemsDirectory, file);
            var templates = ItemTemplateYamlDeserializer.DeserializeFromFile(file, relativePath);
            sources.AddRange(templates.Select(template => new ItemTemplateSource(relativePath, template)));
        }

        ItemTemplateValidator.Validate(sources);

        foreach (var source in sources)
        {
            _templates.Register(source.Template);
        }

        _logger.Information(
            "Loaded {TemplateCount} item template(s) from {YamlFileCount} YAML file(s) in {Path}",
            sources.Count,
            files.Length,
            itemsDirectory
        );

        return ValueTask.CompletedTask;
    }
}
