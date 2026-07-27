using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Handlers;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Handlers;

public class DoubleClickHandlerTests
{
    [Fact]
    public void Publish_ItemTarget_RaisesItemDoubleClickEvent()
    {
        var bus = new StubEventBus();

        DoubleClickHandler.Publish(7, new(0x40000005), bus);

        var evt = Assert.IsType<ItemDoubleClickEvent>(Assert.Single(bus.Published));
        Assert.Equal(7, evt.SessionId);
        Assert.Equal(0x40000005u, evt.Serial.Value);
    }

    [Fact]
    public void Publish_MobileTarget_RaisesMobileDoubleClickEvent()
    {
        var bus = new StubEventBus();

        DoubleClickHandler.Publish(42, new(0x100), bus);

        var evt = Assert.IsType<MobileDoubleClickEvent>(Assert.Single(bus.Published));
        Assert.Equal(42, evt.SessionId);
        Assert.Equal(0x100u, evt.Serial.Value);
    }
}
