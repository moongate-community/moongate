using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json;
using Moongate.UO.Data.Json.Context;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Represents ContainersDataLoader.
/// </summary>
[RegisterFileLoader(11)]
public class ContainersDataLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ILogger _logger = Log.ForContext<ContainersDataLoader>();

    public ContainersDataLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task LoadAsync()
    {
        var containersDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "containers");

        var files = Directory.GetFiles(containersDirectory, "*.json");
        ContainerLayoutSystem.ContainerSizes.Clear();
        ContainerLayoutSystem.ContainerSizesById.Clear();
        ContainerLayoutSystem.ContainerBagDefsByItemId.Clear();
        ContainerLayoutSystem.ContainerBagDefsById.Clear();

        foreach (var containerFile in files)
        {
            var container = JsonUtils.DeserializeFromFile<JsonContainerSize[]>(
                containerFile,
                MoongateUOJsonSerializationContext.Default
            );

            foreach (var containerSize in container)
            {
                _logger.Debug("Adding {JsonContainerSize}", containerSize);
                UpsertContainerBagDefinition(
                    new()
                    {
                        Id = containerSize.Id,
                        ItemId = containerSize.ItemId,
                        Name = containerSize.Name,
                        Width = containerSize.Width,
                        Height = containerSize.Height
                    }
                );
            }
        }

        var cfgPath = Path.Combine(containersDirectory, "containers.cfg");

        if (File.Exists(cfgPath))
        {
            LoadContainersCfg(cfgPath);
        }

        RebuildContainerSizes();
    }

    private void LoadContainersCfg(string cfgPath)
    {
        foreach (var rawLine in File.ReadLines(cfgPath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                continue;
            }

            var gumpId = ParseInteger(parts[0]);
            var rectParts = parts[1].Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (rectParts.Length < 4)
            {
                continue;
            }

            var bounds = new Rectangle2D(
                ParseInteger(rectParts[0]),
                ParseInteger(rectParts[1]),
                ParseInteger(rectParts[2]),
                ParseInteger(rectParts[3])
            );
            var dropSound = ParseInteger(parts[2]);
            var itemIds = parts[3].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var itemIdText in itemIds)
            {
                var itemId = ParseInteger(itemIdText);

                if (!ContainerLayoutSystem.ContainerBagDefsByItemId.TryGetValue(itemId, out var existing))
                {
                    existing = new()
                    {
                        Id = $"item_{itemId:x4}",
                        ItemId = itemId,
                        Name = $"Container 0x{itemId:X4}",
                        Width = 7,
                        Height = 4
                    };
                }

                existing.GumpId = gumpId;
                existing.DropSound = dropSound;
                existing.Bounds = bounds;
                UpsertContainerBagDefinition(existing);
            }
        }
    }

    private static int ParseInteger(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(trimmed[2..], 16);
        }

        return int.Parse(trimmed);
    }

    private static void RebuildContainerSizes()
    {
        ContainerLayoutSystem.ContainerSizes.Clear();
        ContainerLayoutSystem.ContainerSizesById.Clear();

        foreach (var definition in ContainerLayoutSystem.ContainerBagDefsByItemId.Values)
        {
            var width = definition.Width > 0 ? definition.Width : 7;
            var height = definition.Height > 0 ? definition.Height : 4;
            var size = new ContainerSize(definition.Id, width, height, definition.Name);
            ContainerLayoutSystem.ContainerSizes[definition.ItemId] = size;
            ContainerLayoutSystem.ContainerSizesById[definition.Id] = size;
        }
    }

    private static void UpsertContainerBagDefinition(ContainerBagDef definition)
    {
        ContainerLayoutSystem.ContainerBagDefsByItemId[definition.ItemId] = definition;
        ContainerLayoutSystem.ContainerBagDefsById[definition.Id] = definition;
    }
}
