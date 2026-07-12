using System.Reflection;
using Moongate.Server.Data.Internal;
using Moongate.Server.Services;
using Moongate.UO.Data.Items;
using SquidStd.Core.Utils;

namespace Moongate.Server.Internal;

public static class ItemTemplateDirectoryMigrator
{
    public static ItemTemplateMigrationResult Migrate(
        Assembly assembly,
        string embeddedDirectory,
        string embeddedNamespace,
        string legacyFile,
        string backupFile,
        string targetDirectory
    )
    {
        var normalizedTarget = Path.GetFullPath(targetDirectory);
        var parentDirectory = Path.GetDirectoryName(normalizedTarget)!;
        var directoryName = Path.GetFileName(normalizedTarget);
        var temporaryDirectory = Path.Combine(parentDirectory, $".{directoryName}-{Guid.NewGuid():N}.tmp");
        var temporaryPrefix = temporaryDirectory + Path.DirectorySeparatorChar;

        if (Directory.Exists(normalizedTarget))
        {
            throw new IOException($"Item template target directory '{normalizedTarget}' already exists.");
        }

        Directory.CreateDirectory(parentDirectory);

        try
        {
            Directory.CreateDirectory(temporaryDirectory);

            var resources = ResourceUtils.GetEmbeddedResourceNames(assembly, embeddedDirectory)
                .Where(resourceName => resourceName.StartsWith(embeddedNamespace + ".", StringComparison.Ordinal))
                .OrderBy(resourceName => resourceName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (resources.Length == 0)
            {
                throw new InvalidDataException(
                    $"No embedded item template resources were found under '{embeddedDirectory}'."
                );
            }

            var standardFiles = new List<(string RelativePath, ItemTemplate[] Templates)>();
            var defaultSources = new List<ItemTemplateSource>();
            var defaultById = new Dictionary<string, ItemTemplate>(StringComparer.OrdinalIgnoreCase);
            var defaultMembership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var observedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceName in resources)
            {
                var relativePath = ResourceUtils.ConvertResourceNameToPath(resourceName, embeddedNamespace);
                var destination = Path.GetFullPath(Path.Combine(temporaryDirectory, relativePath));

                if (string.Equals(destination, temporaryDirectory, StringComparison.Ordinal) ||
                    !destination.StartsWith(temporaryPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Embedded resource '{resourceName}' resolves outside item template root '{normalizedTarget}'."
                    );
                }

                if (!observedRelativePaths.Add(relativePath))
                {
                    throw new InvalidDataException(
                        $"Duplicate embedded item template path '{relativePath}'."
                    );
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(
                    destination,
                    ResourceUtils.GetEmbeddedResourceByteArray(assembly, resourceName).ToArray()
                );

                var templates = ItemTemplateYamlDeserializer.DeserializeFromFile(destination, relativePath);
                standardFiles.Add((relativePath, templates));

                foreach (var template in templates)
                {
                    if (!defaultById.TryAdd(template.Id, template))
                    {
                        throw new InvalidDataException(
                            $"Duplicate embedded item template ID '{template.Id}' in '{relativePath}' " +
                            $"and '{defaultMembership[template.Id]}'."
                        );
                    }

                    defaultMembership.Add(template.Id, relativePath);
                    defaultSources.Add(new ItemTemplateSource(relativePath, template));
                }
            }

            ItemTemplateValidator.Validate(defaultSources);

            var legacyRelativePath = Path.GetFileName(legacyFile);
            var legacyTemplates = ItemTemplateYamlDeserializer.DeserializeFromFile(legacyFile, legacyRelativePath);
            var legacySources = legacyTemplates
                .Select(template => new ItemTemplateSource(legacyRelativePath, template))
                .ToArray();
            ItemTemplateValidator.Validate(legacySources);

            var legacyById = legacyTemplates.ToDictionary(template => template.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var (relativePath, templates) in standardFiles)
            {
                var merged = templates
                    .Select(template => legacyById.GetValueOrDefault(template.Id) ?? template)
                    .ToArray();
                ItemTemplateYamlSerializer.SerializeToFile(
                    Path.Combine(temporaryDirectory, relativePath),
                    merged
                );
            }

            var customTemplates = legacyTemplates
                .Where(template => !defaultById.ContainsKey(template.Id))
                .OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(template => template.Id, StringComparer.Ordinal)
                .ToArray();

            if (customTemplates.Length > 0)
            {
                const string customRelativePath = "custom.yaml";

                if (observedRelativePaths.Contains(customRelativePath))
                {
                    throw new InvalidDataException(
                        $"Embedded item template path '{customRelativePath}' conflicts with migration output."
                    );
                }

                ItemTemplateYamlSerializer.SerializeToFile(
                    Path.Combine(temporaryDirectory, customRelativePath),
                    customTemplates
                );
            }

            var generatedFiles = Directory.GetFiles(temporaryDirectory, "*.yaml", SearchOption.AllDirectories)
                .OrderBy(file => Path.GetRelativePath(temporaryDirectory, file), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var generatedSources = new List<ItemTemplateSource>();

            foreach (var generatedFile in generatedFiles)
            {
                var relativePath = Path.GetRelativePath(temporaryDirectory, generatedFile);
                var templates = ItemTemplateYamlDeserializer.DeserializeFromFile(generatedFile, relativePath);
                generatedSources.AddRange(
                    templates.Select(template => new ItemTemplateSource(relativePath, template))
                );
            }

            ItemTemplateValidator.Validate(generatedSources);

            if (!File.Exists(backupFile))
            {
                File.Copy(legacyFile, backupFile, overwrite: false);
            }
            else if (!File.ReadAllBytes(backupFile).AsSpan().SequenceEqual(File.ReadAllBytes(legacyFile)))
            {
                throw new InvalidDataException(
                    "Existing item template migration backup differs from legacy source."
                );
            }

            Directory.Move(temporaryDirectory, normalizedTarget);
            File.Delete(legacyFile);

            return new ItemTemplateMigrationResult(
                defaultSources.Count,
                customTemplates.Length,
                generatedFiles.Length,
                backupFile
            );
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
