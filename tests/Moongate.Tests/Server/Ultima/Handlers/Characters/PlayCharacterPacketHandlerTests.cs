using Moongate.Core.Primitives;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Tests.Support.Sessions;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Characters;

public sealed class PlayCharacterPacketHandlerTests
{
    [Fact]
    public async Task Handle_DoesNotEnterTheWorldYet()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        var handler = new PlayCharacterPacketHandler();

        await fixture.ExecuteOnLoopAsync(() =>
            handler.Handle(session, new() { Name = "Aria", ClientFlags = ClientFlags.None, LoginCount = 1, CharacterIndex = 0 })
        );

        Assert.Equal(Serial.Zero, session.CharacterId);
        Assert.True(fixture.Client.IsConnected);
    }
}
