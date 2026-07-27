using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class OplServiceTests
{
    private static readonly int[] _stringClilocs = [1042971, 1070722, 1114057, 1114778, 1114779];

    [Fact]
    public void GetOrBuild_CachesUntilInvalidate()
    {
        var (opl, items, _) = Build();
        var item = NewItem(items, name: "a dagger");
        var cached = opl.GetOrBuild(item.Id);

        item.Name = "a katana";

        Assert.Same(cached, opl.GetOrBuild(item.Id));

        opl.Invalidate(item.Id);

        Assert.Equal("a katana", opl.GetOrBuild(item.Id).Entries[0].Arguments);
    }

    [Fact]
    public void Hash_IsStableForSameContent_AndChangesWithContent()
    {
        var (opl, items, _) = Build();
        var item = NewItem(items, name: "a dagger");

        var first = opl.GetOrBuild(item.Id).Hash;
        opl.Invalidate(item.Id);
        var second = opl.GetOrBuild(item.Id).Hash;

        Assert.Equal(first, second);

        item.Name = "a katana";
        opl.Invalidate(item.Id);

        Assert.NotEqual(first, opl.GetOrBuild(item.Id).Hash);
    }

    [Fact]
    public void Item_CommonRarity_HasNoRarityLine()
    {
        var (opl, items, _) = Build();
        var item = NewItem(items, name: "a dagger");

        var snapshot = opl.GetOrBuild(item.Id);

        Assert.DoesNotContain(snapshot.Entries, e => e.Arguments == "Common");
    }

    [Fact]
    public void Item_Single_UsesStringClilocName()
    {
        var (opl, items, _) = Build();
        var item = NewItem(items);

        var snapshot = opl.GetOrBuild(item.Id);

        Assert.Equal(_stringClilocs[0], snapshot.Entries[0].Cliloc);
        Assert.Equal("a dagger", snapshot.Entries[0].Arguments);
    }

    [Fact]
    public void Item_Stack_UsesAmountCliloc()
    {
        var (opl, items, _) = Build();
        var item = NewItem(items, 3, "gold coin");

        var snapshot = opl.GetOrBuild(item.Id);

        Assert.Equal(1050039, snapshot.Entries[0].Cliloc);
        Assert.Equal("3\tgold coin", snapshot.Entries[0].Arguments);
    }

    [Fact]
    public void Item_WithDescriptionAndRarity_AddsRawLinesOnRotatedClilocs()
    {
        var (opl, items, _) = Build();
        var item = NewItem(items, name: "a dagger");
        item.Description = "shiny";
        item.Rarity = ItemRarityType.Rare;

        var snapshot = opl.GetOrBuild(item.Id);

        Assert.Equal(3, snapshot.Entries.Count);
        Assert.Equal(_stringClilocs[0], snapshot.Entries[0].Cliloc); // name
        Assert.Equal(_stringClilocs[1], snapshot.Entries[1].Cliloc); // description
        Assert.Equal("shiny", snapshot.Entries[1].Arguments);
        Assert.Equal(_stringClilocs[2], snapshot.Entries[2].Cliloc); // rarity
        Assert.Equal("Rare", snapshot.Entries[2].Arguments);
    }

    [Fact]
    public void Item_WithTemplateWeight_AddsWeightLine()
    {
        var (opl, items, templates) = Build();
        templates.Register(new() { Id = "dagger", Name = "a dagger", Weight = 5 });
        templates.Register(new() { Id = "feather", Name = "a feather", Weight = 1 });
        var heavy = NewItem(items, templateId: "dagger");
        var light = NewItem(items, templateId: "feather");

        Assert.Contains(new(1072789, "5"), opl.GetOrBuild(heavy.Id).Entries);
        Assert.Contains(new(1072788, "1"), opl.GetOrBuild(light.Id).Entries);
    }

    [Fact]
    public void ItemServiceDelete_DropsTheCachedList()
    {
        var persistence = new FakePersistenceService();
        var opl = new OplService(persistence, new ItemTemplateService());
        var items = new ItemService(persistence, opl);
        var item = new ItemEntity { Name = "a dagger", ItemId = 3921 };
        items.Create(item);
        Assert.True(opl.GetOrBuild(item.Id).HasEntries);

        items.Delete(item.Id);

        Assert.False(opl.GetOrBuild(item.Id).HasEntries);
    }

    [Fact]
    public void ItemServiceSave_InvalidatesTheCachedList()
    {
        var persistence = new FakePersistenceService();
        var opl = new OplService(persistence, new ItemTemplateService());
        var items = new ItemService(persistence, opl);
        var item = new ItemEntity { Name = "a dagger", ItemId = 3921 };
        items.Create(item);
        Assert.Equal("a dagger", opl.GetOrBuild(item.Id).Entries[0].Arguments);

        item.Name = "a katana";
        items.Save(item);

        Assert.Equal("a katana", opl.GetOrBuild(item.Id).Entries[0].Arguments);
    }

    [Fact]
    public async Task Mobile_BuildsNameLineWithSpaceSlots()
    {
        var (opl, _, _, persistence) = BuildFull();
        var mobile = new MobileEntity { Id = new(0x00000042), Name = "Kandor" };
        await persistence.Store<MobileEntity>().UpsertAsync(mobile);

        var snapshot = opl.GetOrBuild(mobile.Id);

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(1050045, entry.Cliloc);
        Assert.Equal(" \tKandor\t ", entry.Arguments);
    }

    [Fact]
    public void UnknownSerial_ReturnsEmptySnapshot()
    {
        var (opl, _, _) = Build();

        var snapshot = opl.GetOrBuild(new(0x40001234));

        Assert.False(snapshot.HasEntries);
        Assert.Equal(0, snapshot.Hash);
    }

    private static (OplService Opl, ItemService Items, ItemTemplateService Templates) Build()
    {
        var (opl, items, templates, _) = BuildFull();

        return (opl, items, templates);
    }

    private static (OplService Opl, ItemService Items, ItemTemplateService Templates, FakePersistenceService Persistence)
        BuildFull()
    {
        var persistence = new FakePersistenceService();
        var templates = new ItemTemplateService();
        var opl = new OplService(persistence, templates);

        return (opl, new(persistence), templates, persistence);
    }

    private static ItemEntity NewItem(ItemService items, int amount = 1, string name = "a dagger", string templateId = "")
    {
        var item = new ItemEntity { Name = name, Amount = amount, TemplateId = templateId, ItemId = 3921 };
        items.Create(item);

        return item;
    }
}
