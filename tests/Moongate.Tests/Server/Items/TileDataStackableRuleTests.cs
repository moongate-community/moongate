using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// The client's tiledata is the authority on what stacks: <see cref="TileFlagType.Generic" /> is UO's
/// pile bit, and a template that disagrees with the shipped client files loses.
/// </summary>
[Collection("UltimaClientData")]
public class TileDataStackableRuleTests
{
    // Kept inside 0-31: UltimaFixtures.BuildTileData() allocates exactly one 32-item old-format group.
    private const int StackableId = 4;
    private const int PlainId = 5;
    private const int UndescribedId = 900;

    [Fact]
    public void Generic_OverridesATemplateThatSaysNo()
        => WithClientFiles(
            () =>
            {
                var rule = new TileDataStackableRule();
                var item = new ItemEntity { ItemId = StackableId };

                Assert.True(rule.IsStackable(item, new() { Stackable = false }));
            }
        );

    // Past the end of the shipped tables there is nothing to defer to, so the template has the say.
    [Theory, InlineData(true, true), InlineData(false, false), InlineData(null, false)]
    public void IdTheClientFilesDoNotDescribe_FallsBackToTheTemplate(bool? stackable, bool expected)
        => WithClientFiles(
            () =>
            {
                var rule = new TileDataStackableRule();
                var item = new ItemEntity { ItemId = UndescribedId };

                Assert.Equal(expected, rule.IsStackable(item, new() { Stackable = stackable }));
            }
        );

    [Fact]
    public void NoGeneric_OverridesATemplateThatSaysYes()
        => WithClientFiles(
            () =>
            {
                var rule = new TileDataStackableRule();
                var item = new ItemEntity { ItemId = PlainId };

                Assert.False(rule.IsStackable(item, new() { Stackable = true }));
            }
        );

    /// <summary>
    /// Loads a tiledata.mul holding one stackable tile and one plain one. TileData is a process-wide
    /// static, which is why this class joins the serialized UltimaClientData collection.
    /// </summary>
    private static void WithClientFiles(Action assert)
    {
        var tileData = UltimaFixtures.BuildTileData();

        UltimaFixtures.SetItem(tileData, StackableId, (uint)TileFlagType.Generic, 0, "gold coin");
        UltimaFixtures.SetItem(tileData, PlainId, (uint)TileFlagType.Surface, 0, "table");

        var dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", tileData));

        try
        {
            Files.SetDirectory(dir);
            TileData.Initialize();

            assert();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
