using Moongate.Server.Data.Internal;
using Moongate.Server.Interfaces;
using Moongate.Server.Internal;
using Moongate.Server.Services;
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
        var templatesDirectory = _directories.RegisterDirectory("templates");
        var itemsDirectory = Path.Combine(templatesDirectory, "items");
        var directoryExists = Directory.Exists(itemsDirectory);

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
