using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SpellCatalogServiceTests
{
    private static SpellCatalogService Create()
    {
        var data = new StubDataLoaderService().With(
            new SpellDefinition { Id = 2, Key = "create_food", Scroll = "createfoodscroll" },
            new SpellDefinition { Id = 1, Key = "clumsy", Scroll = "clumsyscroll" }
        );
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "clumsyscroll", ItemId = new Serial(0x1F2E) },
                new ItemTemplate { Id = "createfoodscroll", ItemId = new Serial(0x1F2F) }
            )
        );

        return new(data, templates);
    }

    [Fact]
    public void All_IsInTheOrderOfTheNumbers()
    {
        Assert.Equal([1, 2], Create().All.Select(spell => spell.Id));
    }

    [Fact]
    public void TryGet_ByNumberKeyAndScrollGraphic_FindsTheSpell()
    {
        var catalog = Create();

        Assert.True(catalog.TryGet(2, out var byId));
        Assert.True(catalog.TryGetByKey("clumsy", out var byKey));
        Assert.True(catalog.TryGetByScrollGraphic(0x1F2F, out var byScroll));
        Assert.Equal("create_food", byId!.Key);
        Assert.Equal(1, byKey!.Id);
        Assert.Equal(2, byScroll!.Id);
    }

    [Fact]
    public void TryGet_Unknown_IsFalse()
    {
        var catalog = Create();

        Assert.False(catalog.TryGet(3, out _));
        Assert.False(catalog.TryGetByKey("none", out _));
        Assert.False(catalog.TryGetByScrollGraphic(1, out _));
    }
}
