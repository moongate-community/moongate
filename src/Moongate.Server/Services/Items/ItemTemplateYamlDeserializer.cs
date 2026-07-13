using Moongate.UO.Data.Items;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Moongate.Server.Services.Items;

internal static class ItemTemplateYamlDeserializer
{
    private static readonly HashSet<string> RootValueProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ItemTemplate.ItemId),
        nameof(ItemTemplate.Hue),
        nameof(ItemTemplate.GoldValue),
        nameof(ItemTemplate.Weight),
        nameof(ItemTemplate.IsMovable),
        nameof(ItemTemplate.Rarity)
    };

    private static readonly HashSet<string> EquipValueProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(EquipSpec.Layer)
    };

    private static readonly HashSet<string> WeaponValueProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(WeaponSpec.LowDamage),
        nameof(WeaponSpec.HighDamage),
        nameof(WeaponSpec.Speed),
        nameof(WeaponSpec.BaseRange),
        nameof(WeaponSpec.MaxRange),
        nameof(WeaponSpec.HitSound),
        nameof(WeaponSpec.MissSound)
    };

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
                                                        .WithDuplicateKeyChecking()
                                                        .WithEnforceNullability()
                                                        .Build();

    public static ItemTemplate[] DeserializeFromFile(string file, string relativePath)
    {
        try
        {
            return Deserialize(File.ReadAllText(file));
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"Failed to deserialize item template YAML '{relativePath}': {exception.Message}",
                exception
            );
        }
    }

    private static ItemTemplate[] Deserialize(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidDataException("Item template YAML document is empty.");
        }

        ValidateRepresentation(yaml);

        var templates = Deserializer.Deserialize<ItemTemplate?[]?>(yaml);

        if (templates is null)
        {
            throw new InvalidDataException("Item template YAML document is null.");
        }

        for (var templateIndex = 0; templateIndex < templates.Length; templateIndex++)
        {
            var template = templates[templateIndex];

            if (template is null)
            {
                throw new InvalidDataException($"Item template element {templateIndex} is null.");
            }

            ValidateTemplateShape(template, templateIndex);
        }

        return [.. templates.Select(template => template!)];
    }

    private static void ValidateRepresentation(string yaml)
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

            var templateId = (GetValue(template, nameof(ItemTemplate.Id)) as YamlScalarNode)?.Value ?? "<unknown>";

            ValidateNullValueProperties(template, RootValueProperties, templateIndex, templateId, null);
            ValidateNestedNullValueProperties(
                template,
                nameof(ItemTemplate.Equip),
                EquipValueProperties,
                templateIndex,
                templateId
            );
            ValidateNestedNullValueProperties(
                template,
                nameof(ItemTemplate.Weapon),
                WeaponValueProperties,
                templateIndex,
                templateId
            );
            ValidateRequiredCollection(template, nameof(ItemTemplate.Tags), templateIndex, templateId);
            ValidateNullCollectionElements(template, nameof(ItemTemplate.Tags), templateIndex, templateId);
            ValidateNullCollectionElements(template, nameof(ItemTemplate.FlippableItemIds), templateIndex, templateId);
            ValidateNullCollectionElements(template, nameof(ItemTemplate.LootTables), templateIndex, templateId);
            ValidateNestedNullCollectionElements(
                template,
                nameof(ItemTemplate.Container),
                nameof(ContainerSpec.Contents),
                templateIndex,
                templateId
            );
            ValidateNullDictionaryValues(template, nameof(ItemTemplate.Params), templateIndex, templateId);
        }
    }

    private static void ValidateTemplateShape(ItemTemplate template, int templateIndex)
    {
        var templateId = string.IsNullOrWhiteSpace(template.Id) ? "<unknown>" : template.Id;

        if (!Enum.IsDefined(template.Rarity))
        {
            throw ShapeError(templateIndex, templateId, nameof(ItemTemplate.Rarity), $"undefined value '{(int)template.Rarity}'");
        }

        if (template.Equip is not null && !Enum.IsDefined(template.Equip.Layer))
        {
            throw ShapeError(templateIndex, templateId, "Equip.Layer", $"undefined value '{(int)template.Equip.Layer}'");
        }

        if (template.Tags is null)
        {
            throw ShapeError(templateIndex, templateId, nameof(ItemTemplate.Tags), "collection is null");
        }

        ValidateReferenceCollection(template.Tags, templateIndex, templateId, nameof(ItemTemplate.Tags));
        ValidateReferenceCollection(template.LootTables, templateIndex, templateId, nameof(ItemTemplate.LootTables));
        ValidateReferenceCollection(
            template.Container?.Contents,
            templateIndex,
            templateId,
            "Container.Contents"
        );

        if (template.Params is null)
        {
            return;
        }

        foreach (var (key, value) in template.Params)
        {
            if (value is null)
            {
                throw ShapeError(templateIndex, templateId, $"Params[{key}]", "value is null");
            }
        }
    }

    private static void ValidateReferenceCollection<T>(
        IReadOnlyList<T>? values,
        int templateIndex,
        string templateId,
        string property
    )
    {
        if (values is null)
        {
            return;
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is null)
            {
                throw ShapeError(templateIndex, templateId, property, $"element at index {index} is null");
            }
        }
    }

    private static void ValidateNullValueProperties(
        YamlMappingNode mapping,
        HashSet<string> properties,
        int templateIndex,
        string templateId,
        string? prefix
    )
    {
        foreach (var (key, value) in mapping.Children)
        {
            if (key is not YamlScalarNode { Value: { } propertyName } ||
                !properties.Contains(propertyName) ||
                value is not YamlScalarNode scalar ||
                !IsYamlNull(scalar))
            {
                continue;
            }

            var qualifiedProperty = prefix is null ? propertyName : $"{prefix}.{propertyName}";
            throw ShapeError(
                templateIndex,
                templateId,
                qualifiedProperty,
                "is null but is non-nullable"
            );
        }
    }

    private static void ValidateNestedNullValueProperties(
        YamlMappingNode template,
        string property,
        HashSet<string> valueProperties,
        int templateIndex,
        string templateId
    )
    {
        if (GetValue(template, property) is YamlMappingNode nested)
        {
            ValidateNullValueProperties(nested, valueProperties, templateIndex, templateId, property);
        }
    }

    private static void ValidateNullCollectionElements(
        YamlMappingNode mapping,
        string property,
        int templateIndex,
        string templateId
    )
    {
        if (GetValue(mapping, property) is not YamlSequenceNode sequence)
        {
            return;
        }

        for (var index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is YamlScalarNode scalar && IsYamlNull(scalar))
            {
                throw ShapeError(templateIndex, templateId, property, $"has a null element at index {index}");
            }
        }
    }

    private static void ValidateRequiredCollection(
        YamlMappingNode mapping,
        string property,
        int templateIndex,
        string templateId
    )
    {
        if (GetValue(mapping, property) is YamlScalarNode scalar && IsYamlNull(scalar))
        {
            throw ShapeError(templateIndex, templateId, property, "collection is null");
        }
    }

    private static void ValidateNestedNullCollectionElements(
        YamlMappingNode template,
        string parentProperty,
        string collectionProperty,
        int templateIndex,
        string templateId
    )
    {
        if (GetValue(template, parentProperty) is YamlMappingNode nested &&
            GetValue(nested, collectionProperty) is YamlSequenceNode sequence)
        {
            for (var index = 0; index < sequence.Children.Count; index++)
            {
                if (sequence.Children[index] is YamlScalarNode scalar && IsYamlNull(scalar))
                {
                    throw ShapeError(
                        templateIndex,
                        templateId,
                        $"{parentProperty}.{collectionProperty}",
                        $"has a null element at index {index}"
                    );
                }
            }
        }
    }

    private static void ValidateNullDictionaryValues(
        YamlMappingNode template,
        string property,
        int templateIndex,
        string templateId
    )
    {
        if (GetValue(template, property) is not YamlMappingNode dictionary)
        {
            return;
        }

        foreach (var (key, value) in dictionary.Children)
        {
            if (value is not YamlScalarNode scalar || !IsYamlNull(scalar))
            {
                continue;
            }

            var dictionaryKey = (key as YamlScalarNode)?.Value ?? "<unknown>";
            throw ShapeError(
                templateIndex,
                templateId,
                $"{property}[{dictionaryKey}]",
                "has a null value"
            );
        }
    }

    private static YamlNode? GetValue(YamlMappingNode mapping, string property)
    {
        foreach (var (key, value) in mapping.Children)
        {
            if (key is YamlScalarNode { Value: { } propertyName } &&
                string.Equals(propertyName, property, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static InvalidDataException ShapeError(
        int templateIndex,
        string templateId,
        string property,
        string message
    )
    {
        return new InvalidDataException(
            $"Item template element {templateIndex} ('{templateId}') property '{property}' {message}."
        );
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
