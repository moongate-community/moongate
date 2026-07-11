using Moongate.UO.Data.Loot;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Moongate.Server.Services;

internal static class LootTemplateYamlDeserializer
{
    private static readonly HashSet<string> NonNullableValueProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(LootTemplate.Mode),
        nameof(LootTemplate.Rolls),
        nameof(LootTemplate.NoDropWeight)
    };

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
                                                        .WithDuplicateKeyChecking()
                                                        .WithEnforceNullability()
                                                        .Build();

    public static LootTemplate[] DeserializeFromFile(string file, string relativePath)
    {
        try
        {
            return Deserialize(File.ReadAllText(file));
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"Failed to deserialize loot template YAML '{relativePath}'.",
                exception
            );
        }
    }

    private static LootTemplate[] Deserialize(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidDataException("Loot template YAML document is empty.");
        }

        ValidateNonNullableValueProperties(yaml);

        var templates = Deserializer.Deserialize<LootTemplate?[]?>(yaml);

        if (templates is null)
        {
            throw new InvalidDataException("Loot template YAML document is null.");
        }

        for (var templateIndex = 0; templateIndex < templates.Length; templateIndex++)
        {
            var template = templates[templateIndex];

            if (template is null)
            {
                throw new InvalidDataException($"Loot template element {templateIndex} is null.");
            }

            if (!Enum.IsDefined(template.Mode))
            {
                throw new InvalidDataException(
                    $"Loot template element {templateIndex} has undefined Mode value '{(int)template.Mode}'."
                );
            }

            if (template.Entries is null)
            {
                throw new InvalidDataException($"Loot template element {templateIndex} has a null Entries collection.");
            }

            for (var entryIndex = 0; entryIndex < template.Entries.Count; entryIndex++)
            {
                if (template.Entries[entryIndex] is null)
                {
                    throw new InvalidDataException(
                        $"Loot template element {templateIndex} has a null entry element at index {entryIndex}."
                    );
                }
            }
        }

        return [.. templates.Select(template => template!)];
    }

    private static void ValidateNonNullableValueProperties(string yaml)
    {
        var yamlStream = new YamlStream();

        using var reader = new StringReader(yaml);
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0 || yamlStream.Documents[0].RootNode is not YamlSequenceNode templates)
        {
            return;
        }

        for (var templateIndex = 0; templateIndex < templates.Children.Count; templateIndex++)
        {
            if (templates.Children[templateIndex] is not YamlMappingNode template)
            {
                continue;
            }

            foreach (var (key, value) in template.Children)
            {
                if (key is not YamlScalarNode { Value: { } propertyName } ||
                    !NonNullableValueProperties.Contains(propertyName) ||
                    value is not YamlScalarNode scalar ||
                    !IsYamlNull(scalar))
                {
                    continue;
                }

                throw new InvalidDataException(
                    $"Loot template element {templateIndex} has null non-nullable property '{propertyName}'."
                );
            }
        }
    }

    private static bool IsYamlNull(YamlScalarNode scalar)
    {
        return scalar.Tag == "tag:yaml.org,2002:null" ||
               (scalar.Style == YamlDotNet.Core.ScalarStyle.Plain &&
                (string.IsNullOrEmpty(scalar.Value) ||
                 scalar.Value == "~" ||
                 string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase)));
    }
}
