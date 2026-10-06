using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class StartingItemsServiceTests
{
    [Theory]
    [InlineData("readable_book", false, true)]
    [InlineData("readable_scroll", true, false)]
    [InlineData("jail_note", true, false)]
    public async Task GiveAsync_LoaderBypassedWithIncompatibleDocument_RejectsBeforePreparingOrSavingDocument(
        string script, bool writable, bool attachments)
    {
        var (service, factory, preparation) = CreateService(script, writable, attachments);

        // The fake factory never accesses the caller's transaction or a database.
        await Assert.ThrowsAsync<InvalidDataException>(() => service.GiveAsync(null!, Request()));

        Assert.Equal(0, preparation.Calls);
        Assert.Equal(["backpack"], factory.CreatedTemplateIds);
        Assert.Equal("backpack", Assert.Single(Assert.Single(factory.Saved)).TemplateId);
    }

    [Theory]
    [InlineData("readable_book", true, false)]
    [InlineData("readable_scroll", false, true)]
    [InlineData("jail_note", false, true)]
    [InlineData("readable_scroll", false, false)]
    [InlineData("readable_book", false, false)]
    public async Task GiveAsync_CompatibleCustomItem_PreservesDocumentCapabilitiesAndCover(
        string script, bool writable, bool attachments)
    {
        var (service, factory, preparation) = CreateService(script, writable, attachments);

        var given = await service.GiveAsync(null!, Request());

        var item = Assert.Single(given.Skip(1));
        Assert.Equal("custom_item", item.TemplateId);
        Assert.Equal(0x0FF1, item.ItemId);
        Assert.Equal("Document", item.Name);
        Assert.Equal("Aria", item.GetProp<string>("book.author"));
        Assert.Equal(writable, BookDocumentText.IsWritable(item));
        Assert.Equal(writable ? 20 : 0, BookDocumentText.PagesOf(item));
        Assert.Equal(1, preparation.Calls);
        Assert.Equal(2, factory.Saved.Count);
        if (attachments)
        {
            Assert.True(BookAttachmentCodec.TryDecode(item.GetProp<string>(BookAttachmentCodec.PropKey), out var batch));
            Assert.Equal(100, Assert.Single(batch!.Items).Amount);
        }
        else
        {
            Assert.False(item.TryGetProp<string>(BookAttachmentCodec.PropKey, out _));
        }
    }

    private static (StartingItemsService Service, FakeItemFactoryService Factory, CountingBookAttachmentPreparationService Preparation) CreateService(
        string script, bool writable, bool attachments)
    {
        var source = new BookTemplate
        {
            Id = "document", Title = "Document", Author = "$player_name", Writable = writable,
            ItemTemplate = writable || !attachments ? "source_book" : "source_scroll", ItemId = 0x0FF1,
            Attachments = attachments ? [new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100") }] : []
        };
        var data = new StubDataLoaderService().With(source).With(
            new ItemTemplate { Id = "backpack", ItemId = new(0x0E75), Stackable = false },
            new ItemTemplate { Id = "gold", ItemId = new(0x0EED), Stackable = true },
            new ItemTemplate { Id = "custom_item", ItemId = new(0x14ED), Stackable = false, ScriptId = script }
        ).With(new StartingItemSet
        {
            Common = true, Items = [new() { Items = ["custom_item"], BookTemplate = "document" }]
        }).With(new ContainerContent { Name = "default", Default = true, Bounds = new(new(44, 65), new(186, 159)) });
        var tiles = new FakeTileDataService();
        var templates = new ItemTemplateService(data);
        var factory = new FakeItemFactoryService(templates, tiles);
        var preparation = new CountingBookAttachmentPreparationService(new BookAttachmentPreparationService(factory, templates, tiles));
        // This test exercises the caller-owned transaction overload, which does not use the persistence service.
        var service = new StartingItemsService(data, factory, templates, new ContainerLayoutService(data), tiles, null!,
            new StartingItemsConfig(), new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = "gold" },
            new BookTemplateService(data), TestBookContexts.Create(), new LocalizationConfig(), preparation);
        return (service, factory, preparation);
    }

    private static StartingItemsRequest Request()
    {
        return new(new(0x100), RaceType.Human, GenderType.Male, new Dictionary<SkillType, int>(), default, default) { PlayerName = "Aria" };
    }
}
