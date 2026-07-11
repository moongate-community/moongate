using Moongate.Server.Data.Internal;
using Moongate.Server.Interfaces;
using Moongate.Server.Services;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;

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
            SeedDefaults(lootDirectory);
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

    private static void SeedDefaults(string lootDirectory)
    {
        var assembly = typeof(LootTemplatesLoader).Assembly;
        var normalizedLootDirectory = Path.GetFullPath(lootDirectory);
        var parentDirectory = Path.GetDirectoryName(normalizedLootDirectory)!;
        var temporaryDirectory = Path.Combine(parentDirectory, $".loot-{Guid.NewGuid():N}.tmp");
        var temporaryDirectoryPrefix = temporaryDirectory + Path.DirectorySeparatorChar;
        var resources = ResourceUtils.GetEmbeddedResourceNames(assembly, EmbeddedDirectory)
            .Where(resourceName => resourceName.StartsWith(EmbeddedNamespace + ".", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        Directory.CreateDirectory(parentDirectory);

        try
        {
            Directory.CreateDirectory(temporaryDirectory);

            foreach (var resourceName in resources)
            {
                var relativePath = ResourceUtils.ConvertResourceNameToPath(resourceName, EmbeddedNamespace);
                var destination = Path.GetFullPath(Path.Combine(temporaryDirectory, relativePath));

                if (string.Equals(destination, temporaryDirectory, StringComparison.Ordinal) ||
                    !destination.StartsWith(temporaryDirectoryPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Embedded loot resource '{resourceName}' resolves outside loot root '{normalizedLootDirectory}'."
                    );
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(
                    destination,
                    ResourceUtils.GetEmbeddedResourceByteArray(assembly, resourceName).ToArray()
                );
            }

            Directory.Move(temporaryDirectory, normalizedLootDirectory);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }

            throw;
        }
    }
}
