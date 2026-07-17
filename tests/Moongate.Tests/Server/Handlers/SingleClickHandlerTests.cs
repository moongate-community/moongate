using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Handlers;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Handlers;

public class SingleClickHandlerTests
{
    [Fact]
    public void Publish_MobileTarget_RaisesMobileSingleClickEvent()
    {
        var bus = new StubEventBus();

        SingleClickHandler.Publish(42, new Serial(0x100), bus);

        var evt = Assert.IsType<MobileSingleClickEvent>(Assert.Single(bus.Published));
        Assert.Equal(42, evt.SessionId);
        Assert.Equal(0x100u, evt.Serial.Value);
    }

    [Fact]
    public void Publish_ItemTarget_RaisesItemSingleClickEvent()
    {
        var bus = new StubEventBus();

        SingleClickHandler.Publish(7, new Serial(0x40000005), bus);

        var evt = Assert.IsType<ItemSingleClickEvent>(Assert.Single(bus.Published));
        Assert.Equal(7, evt.SessionId);
        Assert.Equal(0x40000005u, evt.Serial.Value);
    }
}
