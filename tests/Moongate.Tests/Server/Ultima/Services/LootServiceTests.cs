using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LootServiceTests
{
    private readonly LootService _loot;

    public LootServiceTests()
    {
        var loaders = new StubDataLoaderService()
                      .With(
                          new ItemTemplate { Id = "ruby", ItemId = new Serial(0x0F13) },
                          new ItemTemplate { Id = "arrow", ItemId = new Serial(0x0F3F) },
                          new ItemTemplate { Id = "bottle", ItemId = new Serial(0x0F0E) }
                      )
                      .With(
                          Table("mostly_nothing", new LootEntry { Weight = 80 }, new LootEntry { Weight = 20, ItemId = "ruby" }),
                          Table("arrows", new LootEntry { ItemId = "arrow", Amount = RangeValueSpec<int>.FromValue(30) }),
                          Table("bottles", new LootEntry { ItemId = "bottle", Amount = RangeValueSpec<int>.FromValue(3) }),
                          Table("nested", new LootEntry { LootTemplateId = "arrows" }),
                          Table("nested_twice", new LootEntry { LootTemplateId = "arrows", Amount = RangeValueSpec<int>.FromValue(2) }),
                          Table("empty")
                      );
        var tiles = new FakeTileDataService().Item(0x0F13, TileFlagType.None, 0)
                                             .Item(0x0F3F, TileFlagType.Generic, 0)
                                             .Item(0x0F0E, TileFlagType.None, 0);
        var itemTemplates = new ItemTemplateService(loaders);

        // Roll only builds items in memory; the factory's persistence is never touched.
        _loot = new LootService(loaders, new ItemFactoryService(itemTemplates, tiles, null!), itemTemplates, tiles);
    }

    [Fact]
    public void Roll_FollowsTheWeights_AndABlankEntryGivesNothing()
    {
        var nothing = Enumerable.Range(0, 1000).Count(_ => _loot.Roll("mostly_nothing").Count == 0);

        Assert.InRange(nothing, 700, 900);
    }

    [Fact]
    public void Roll_AStackableItemIsOnePile_AndAnyOtherIsSplit()
    {
        Assert.Equal(30, Assert.Single(_loot.Roll("arrows")).Amount);
        Assert.Equal([1, 1, 1], _loot.Roll("bottles").Select(item => item.Amount));
    }

    [Fact]
    public void Roll_ANestedTable_RollsIt_AndAnUnknownTableThrows()
    {
        Assert.Equal("arrow", Assert.Single(_loot.Roll("nested")).TemplateId);
        Assert.Contains("'gems'", Assert.Throws<KeyNotFoundException>(() => _loot.Roll("gems")).Message);
        Assert.Empty(_loot.Roll("empty"));
    }

    [Fact]
    public void Roll_ANestedEntryWithAnAmount_RollsTheTableThatManyTimes()
    {
        // UOX3: LOOTLIST=randomgems,2 rolls randomgems twice.
        Assert.Equal([30, 30], _loot.Roll("nested_twice").Select(item => item.Amount));
    }

    private static LootTemplate Table(string id, params LootEntry[] entries)
    {
        return new() { Id = id, Entries = [..entries] };
    }
}
