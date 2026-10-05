using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.HuePicking;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Ultima.HuePicking;

namespace Moongate.Tests.Server.Ultima.Handlers.HuePicking;

public sealed class HuePickerResponsePacketHandlerTests
{
    [Fact]
    public async Task Handle_GivesTheIdAndTheHueOfTheClientToThePickerService()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        var pickers = new StubHuePickerService();

        new HuePickerResponsePacketHandler(pickers).Handle(session, new() { PickerId = 5, Hue = 0x0026 });

        Assert.Equal([(5, 0x0026)], pickers.Completed);
    }
}
