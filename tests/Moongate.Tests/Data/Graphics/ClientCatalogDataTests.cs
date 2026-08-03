using Moongate.Http.Plugin.Data.Api.Graphics;
using Moongate.Http.Plugin.Endpoints.Graphics;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Data.Graphics;

/// <summary>
/// The two client catalogues against a real client directory. Unit tests can prove the projections are
/// consistent with themselves; only the files can say whether what comes out is the hue and the tile a
/// player would recognise.
/// </summary>
public class ClientCatalogDataTests
{
    [ClientFilesFact]
    public void Hues_TheTableIsRead()
    {
        Load();

        var loaded = Hues.List.Count(HueEndpoints.IsLoaded);

        // hues.mul ships a few thousand; the exact count varies by client, so this only asserts the
        // file was read at all rather than pinning a number a client update would break.
        Assert.InRange(loaded, 1000, 3000);
    }

    [ClientFilesFact]
    public void Hues_EverySwatchIsAColourAndNotBlack()
    {
        Load();

        var swatches = Hues.List
                           .Where(HueEndpoints.IsLoaded)
                           .Select((hue, index) => HueSummary.From(index + 1, hue))
                           .ToArray();

        Assert.All(swatches, swatch => Assert.Matches("^#[0-9A-F]{6}$", swatch.Hex));
        Assert.DoesNotContain(swatches, swatch => swatch.Hex == "#000000");
    }

    [ClientFilesFact]
    public void UoItems_GoldIsAStackableTileCalledGoldCoin()
    {
        Load();

        var gold = UoItemSummary.From(3821, TileData.ItemTable[3821]);

        Assert.Contains("gold", gold.Name, StringComparison.OrdinalIgnoreCase);

        // Generic is UO's "this is a pile" bit, which is what makes coins stack.
        Assert.Contains(nameof(TileFlagType.Generic), gold.Flags);
    }

    [ClientFilesFact]
    public void UoItems_TheBackpackIsAContainer()
    {
        Load();

        Assert.Contains(nameof(TileFlagType.Container), UoItemSummary.From(3701, TileData.ItemTable[3701]).Flags);
    }

    // The search is the only way into a table this size, so it has to reach a tile by the three things
    // someone would actually type.
    [ClientFilesFact]
    public void UoItems_GoldIsFoundByName_ByDecimalId_AndByHexId()
    {
        Load();

        var gold = TileData.ItemTable[3821];

        Assert.True(UoItemEndpoints.Matches(3821, gold, "gold"));
        Assert.True(UoItemEndpoints.Matches(3821, gold, "3821"));
        Assert.True(UoItemEndpoints.Matches(3821, gold, "0xEED"));
    }

    /// <summary>
    /// Points the readers at the client directory and fills the two process-wide tables. Both are
    /// idempotent, and the tables are shared with every other test in the run, so this only ever puts
    /// back data that was going to be there.
    /// </summary>
    private static void Load()
    {
        Files.SetDirectory(ClientFiles.Directory);
        TileData.Initialize();
        Hues.Initialize();
    }
}
