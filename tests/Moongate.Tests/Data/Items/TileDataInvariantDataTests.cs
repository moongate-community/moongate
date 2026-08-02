using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;
using SquidStd.Core.Yaml;

namespace Moongate.Tests.Data.Items;

/// <summary>
/// The shipped templates against a real client's tiledata. A template that declares what the client
/// already says is not an override, it is a duplicate waiting to drift — these keep them from
/// creeping back as content grows. Both skip where there is no client directory.
/// </summary>
[Collection("UltimaClientData")]
public class TileDataInvariantDataTests
{
    [ClientFilesFact]
    public async Task NoTemplate_DeclaresALayerTheClientAlreadyGives()
    {
        var redundant = new List<string>();

        foreach (var template in await DeclaredTemplates())
        {
            if (template.Equip is not { Layer: not LayerType.None } equip || Tile(template.ItemId) is not { } tile)
            {
                continue;
            }

            if (TileDataTemplateResolver.LayerFor(tile.Wearable, tile.Quality) == equip.Layer)
            {
                redundant.Add($"{template.Id} ({equip.Layer})");
            }
        }

        Assert.True(
            redundant.Count == 0,
            $"Templates declaring a layer the tiledata already gives: {string.Join(", ", redundant)}"
        );
    }

    [ClientFilesFact]
    public async Task NoTemplate_DeclaresAWeightTheClientAlreadyGives()
    {
        var redundant = new List<string>();

        foreach (var template in await DeclaredTemplates())
        {
            // Zero is "not stated", not a declaration, so it cannot be redundant.
            if (template.Weight == 0 || Tile(template.ItemId) is not { } tile)
            {
                continue;
            }

            if (Math.Abs(template.Weight - TileDataTemplateResolver.WeightFor(tile.Weight)) < 0.001)
            {
                redundant.Add($"{template.Id} ({template.Weight})");
            }
        }

        Assert.True(
            redundant.Count == 0,
            $"Templates declaring a weight the tiledata already gives: {string.Join(", ", redundant)}"
        );
    }

    /// <summary>
    /// The templates as the YAML declares them. The loader resolves what it registers, which would
    /// hide exactly what these tests look for, so this reads the seeded files instead of asking the
    /// registry.
    /// </summary>
    private static async Task<IReadOnlyList<ItemTemplate>> DeclaredTemplates()
    {
        Files.SetDirectory(ClientFiles.Directory);
        TileData.Initialize();

        var root = Path.Combine(Path.GetTempPath(), "mg-tiledata-inv-" + Guid.NewGuid().ToString("N"));

        await new ItemTemplatesLoader(new ItemTemplateService(), new(root, [])).LoadAsync();

        try
        {
            return Directory.GetFiles(Path.Combine(root, "templates", "items"), "*.yaml", SearchOption.AllDirectories)
                            .SelectMany(path => YamlUtils.DeserializeFromFile<ItemTemplate[]>(path) ?? [])
                            .ToList();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static ItemData? Tile(int itemId)
        => TileData.ItemTable is { Length: > 0 } tiles && itemId >= 0 && itemId < tiles.Length
               ? tiles[itemId]
               : null;
}
