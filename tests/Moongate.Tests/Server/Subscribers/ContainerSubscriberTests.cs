using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.UO.Data.Containers;

namespace Moongate.Tests.Server.Subscribers;

public class ContainerSubscriberTests
{
    [Theory, InlineData(0), InlineData(-5)]

    // an item that never had its amount set
    // nonsense, but the wire field is unsigned
    public void BuildContents_AmountBelowOne_IsSentAsOne(int amount)
    {
        var item = new ItemEntity { Id = new(0x40000005), ItemId = 1, Amount = amount };

        Assert.Equal(1, Assert.Single(ContainerSubscriber.BuildContents([item])).Amount);
    }

    [Fact]
    public void BuildContents_CarriesEachItemsGraphicSlotAndHue()
    {
        var dagger = new ItemEntity
        {
            Id = new(0x40000005),
            ItemId = 0x0F51,
            Amount = 3,
            ContainerPosition = new(44, 65),
            Hue = new(0x21)
        };

        var entry = Assert.Single(ContainerSubscriber.BuildContents([dagger]));

        Assert.Equal(0x40000005u, entry.Serial.Value);
        Assert.Equal(0x0F51, entry.ItemId);
        Assert.Equal(3, entry.Amount);
        Assert.Equal(44, entry.Position.X);
        Assert.Equal(65, entry.Position.Y);
        Assert.Equal(0x21, entry.Hue.Value);
    }

    [Fact]
    public void BuildContents_EmptyContainer_ProducesNoEntries()
        => Assert.Empty(ContainerSubscriber.BuildContents([]));

    [Fact]
    public void BuildContents_KeepsTheOrderItemsCameIn()
    {
        var first = new ItemEntity { Id = new(0x40000005), ItemId = 1, Amount = 1 };
        var second = new ItemEntity { Id = new(0x40000006), ItemId = 2, Amount = 1 };

        var contents = ContainerSubscriber.BuildContents([first, second]);

        Assert.Equal(0x40000005u, contents[0].Serial.Value);
        Assert.Equal(0x40000006u, contents[1].Serial.Value);
    }

    [Fact]
    public void ResolveGumpId_ContainerGraphicButNoContainerBlock_StaysShut()

        // The key ring's case: a container to the client, an Item to ModernUO. The template decides.
        => Assert.Null(Subscriber().ResolveGumpId(Item("key_ring", 4113)));

    [Fact]
    public void ResolveGumpId_ItemMadeWithoutATemplate_IsNotAContainer()
        => Assert.Null(Subscriber().ResolveGumpId(Item(string.Empty, 3701)));

    [Fact]
    public void ResolveGumpId_NeitherTemplateNorTableNamesOne_FallsBackToThePlainBag()

        // The backpack's own case: listed in neither, so it lands on the table's default entry.
        => Assert.Equal(ContainerGumpLayout.DefaultGumpId, Subscriber().ResolveGumpId(Item("backpack", 3701)));

    [Fact]
    public void ResolveGumpId_TemplateHasNoContainerBlock_IsNotAContainer()
        => Assert.Null(Subscriber().ResolveGumpId(Item("dagger", 3921)));

    [Fact]
    public void ResolveGumpId_TemplateNamesItsOwnGump_ThatGumpWins()
        => Assert.Equal(74, Subscriber().ResolveGumpId(Item("bank_box", 2475)));

    [Fact]
    public void ResolveGumpId_TemplateNamesNoGump_TakesTheOneTheGumpTableGivesTheGraphic()

        // Mirrors ModernUO: an un-overridden DefaultGumpID falls through to ContainerData.GetData(itemID).
        => Assert.Equal(65, Subscriber().ResolveGumpId(Item("basket_artifact", 9437)));

    [Fact]
    public void ResolveGumpId_UnknownTemplate_IsNotAContainer()
        => Assert.Null(Subscriber().ResolveGumpId(Item("does_not_exist", 3701)));

    private static ItemEntity Item(string templateId, int itemId)
        => new() { Id = new(0x40000005), TemplateId = templateId, ItemId = itemId, Amount = 1 };

    private static ContainerSubscriber Subscriber()
    {
        var templates = new ItemTemplateService();
        templates.Register(new() { Id = "dagger", Name = "Dagger", Category = "Weapons", ItemId = 3921 });
        templates.Register(new() { Id = "key_ring", Name = "Key Ring", Category = "Misc", ItemId = 4113 });
        templates.Register(
            new() { Id = "backpack", Name = "Backpack", Category = "Containers", ItemId = 3701, Container = new() }
        );
        templates.Register(
            new()
            {
                Id = "bank_box", Name = "Bank Box", Category = "Containers", ItemId = 2475,
                Container = new() { GumpId = 74 }
            }
        );
        templates.Register(
            new()
            {
                Id = "basket_artifact", Name = "Basket", Category = "Decoration Artifacts", ItemId = 9437,
                Container = new()
            }
        );

        var gumps = new ContainerGumpService();
        gumps.Register(new() { GumpId = 65, ItemIds = [9437] });

        return new(
            new StubSessionManager(),
            new StubItemService([]),
            templates,
            gumps,
            new OplService(new FakePersistenceService(), templates)
        );
    }
}
