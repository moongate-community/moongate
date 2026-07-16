using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Subscribers;
using Moongate.UO.Data.Hues;

namespace Moongate.Tests.Server.Subscribers;

public class ContainerSubscriberTests
{
    [Fact]
    public void BuildContents_CarriesEachItemsGraphicSlotAndHue()
    {
        var dagger = new ItemEntity
        {
            Id = new Serial(0x40000005),
            ItemId = 0x0F51,
            Amount = 3,
            ContainerPosition = new Point2D(44, 65),
            Hue = new Hue(0x21)
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

    [Theory]
    [InlineData(0)]  // an item that never had its amount set
    [InlineData(-5)] // nonsense, but the wire field is unsigned
    public void BuildContents_AmountBelowOne_IsSentAsOne(int amount)
    {
        var item = new ItemEntity { Id = new Serial(0x40000005), ItemId = 1, Amount = amount };

        Assert.Equal(1, Assert.Single(ContainerSubscriber.BuildContents([item])).Amount);
    }

    [Fact]
    public void BuildContents_KeepsTheOrderItemsCameIn()
    {
        var first = new ItemEntity { Id = new Serial(0x40000005), ItemId = 1, Amount = 1 };
        var second = new ItemEntity { Id = new Serial(0x40000006), ItemId = 2, Amount = 1 };

        var contents = ContainerSubscriber.BuildContents([first, second]);

        Assert.Equal(0x40000005u, contents[0].Serial.Value);
        Assert.Equal(0x40000006u, contents[1].Serial.Value);
    }
}
