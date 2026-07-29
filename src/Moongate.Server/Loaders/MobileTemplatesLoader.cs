using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Internal;
using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Server.Loaders;

/// <summary>Seeds and loads mobile spawn templates from the recursive mobile template directory.</summary>
public sealed class MobileTemplatesLoader : IDataLoader
{
    private const string EmbeddedDirectory = "Assets/Templates/Mobiles";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Templates.Mobiles";

    private readonly ILogger _logger = Log.ForContext<MobileTemplatesLoader>();
    private readonly IMobileTemplateService _templates;
    private readonly DirectoriesConfig _directories;
    private readonly INameService _names;
    private readonly MobileTemplateBaseResolver _resolver = new();

    public MobileTemplatesLoader(
        IMobileTemplateService templates,
        DirectoriesConfig directories,
        INameService names
    )
    {
        _templates = templates;
        _directories = directories;
        _names = names;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var templatesDirectory = _directories.RegisterDirectory("templates");
        var mobilesDirectory = Path.Combine(templatesDirectory, "mobiles");

        if (!Directory.Exists(mobilesDirectory))
        {
            EmbeddedResourceDirectorySeeder.SeedAtomic(
                typeof(MobileTemplatesLoader).Assembly,
                EmbeddedDirectory,
                EmbeddedNamespace,
                mobilesDirectory
            );
        }

        if (!Directory.Exists(mobilesDirectory))
        {
            return ValueTask.CompletedTask;
        }

        var files = Directory.GetFiles(mobilesDirectory, "*.yaml", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

        if (files.Length == 0)
        {
            _logger.Warning("No mobile template YAML files found in {Path}", mobilesDirectory);

            return ValueTask.CompletedTask;
        }

        var parsed = new List<MobileTemplate>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(mobilesDirectory, file);
            parsed.AddRange(MobileTemplateYamlDeserializer.DeserializeFromFile(file, relativePath));
        }

        var resolved = _resolver.Resolve(parsed);

        foreach (var template in resolved)
        {
            ValidateNamePools(template);
            _templates.Register(template);
        }

        _logger.Information(
            "Loaded {TemplateCount} mobile template(s) from {YamlFileCount} YAML file(s) in {Path}",
            resolved.Count,
            files.Length,
            mobilesDirectory
        );

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A NamePool naming no registered pool is a content typo. Names load at priority 30 and mobile
    /// templates at 150, so it is caught while the server is still starting instead of producing
    /// nameless NPCs long after. Runs after base resolution, so an inherited pool is checked on each
    /// derived template rather than only on the base.
    /// </summary>
    private void ValidateNamePools(MobileTemplate template)
    {
        RequirePool(template.Id, template.NamePool);

        foreach (var variant in template.Variants)
        {
            RequirePool(template.Id, variant.NamePool);
        }
    }

    private void RequirePool(string templateId, string? pool)
    {
        if (string.IsNullOrEmpty(pool) || _names.GetByType(pool) is not null)
        {
            return;
        }

        throw new InvalidDataException(
            $"Mobile template '{templateId}' references unknown name pool '{pool}'."
        );
    }
}
