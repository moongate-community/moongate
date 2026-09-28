using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class ItemEntityExtensionsTests
{
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
            new ItemTemplate { Id = "bread", ItemId = new Serial(0x103B) },
            new ItemTemplate { Id = "sword_of_ages", ItemId = new Serial(0x13B9), Name = "Sword of Ages" },
            new ItemTemplate { Id = "odd", ItemId = new Serial(0x0001) }
        )
    );

    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                  .Item(0x0EED, TileFlagType.Generic, 0, name: "gold coin%s%")
                                                  .Item(0x103B, TileFlagType.None, 0, name: "loa%ves/f% of bread")
                                                  .Item(0x13B9, TileFlagType.Wearable, 0, name: "longsword");

    [Fact]
    public void DisplayName_OwnName_Wins()
    {
        Assert.Equal("Excalibur", Item("sword_of_ages", 0x13B9, name: "Excalibur").DisplayName(_templates, _tiles));
    }

    [Fact]
    public void DisplayName_WithoutOwnName_UsesTheTemplateName()
    {
        Assert.Equal("Sword of Ages", Item("sword_of_ages", 0x13B9).DisplayName(_templates, _tiles));
    }

    [Theory]
    [InlineData(1, "gold coin")]
    [InlineData(250, "gold coins")]
    public void DisplayName_WithoutAnyName_UsesTheClientNameForTheAmount(int amount, string expected)
    {
        Assert.Equal(expected, Item("gold", 0x0EED, amount).DisplayName(_templates, _tiles));
    }

    [Theory]
    [InlineData(1, "loaf of bread")]
    [InlineData(3, "loaves of bread")]
    public void DisplayName_PluralMarkerWithASingularForm_PicksTheRightOne(int amount, string expected)
    {
        Assert.Equal(expected, Item("bread", 0x103B, amount).DisplayName(_templates, _tiles));
    }

    [Fact]
    public void DisplayName_GraphicWithoutAClientName_FallsBackToTheTemplateId()
    {
        Assert.Equal("odd", Item("odd", 0x0001).DisplayName(_templates, _tiles));
    }

    [Fact]
    public void DisplayName_UnknownTemplate_UsesTheGraphic()
    {
        Assert.Equal("gold coin", Item("deleted_template", 0x0EED).DisplayName(_templates, _tiles));
    }

    private static ItemEntity Item(string templateId, int itemId, int amount = 1, string? name = null)
    {
        return new() { TemplateId = templateId, ItemId = itemId, Amount = amount, Name = name };
    }
}
