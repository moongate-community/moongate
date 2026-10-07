using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class WarModeRequestPacketHandlerTests : IAsyncLifetime
{
    private readonly RecordingMobileStateService _state = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Handle_PutsTheCharacterInTheModeAsked(bool warMode)
    {
        new WarModeRequestPacketHandler(_fixture.Mobiles, _state).Handle(
            _session,
            new WarModeRequestPacket { WarMode = warMode }
        );

        Assert.Equal([$"war 2 {warMode}"], _state.Flags);
    }

    [Fact]
    public void Handle_PeaceEndsTheFightOfTheCharacter_WarDoesNot()
    {
        var combat = new Moongate.Tests.TestSupport.Ultima.Combat.RecordingCombatService();
        var handler = new WarModeRequestPacketHandler(_fixture.Mobiles, _state, combat);

        handler.Handle(_session, new WarModeRequestPacket { WarMode = true });
        Assert.Empty(combat.Stopped);

        handler.Handle(_session, new WarModeRequestPacket { WarMode = false });

        Assert.Equal(new Serial(2), Assert.Single(combat.Stopped).Id);
    }

    [Fact]
    public void Handle_WithoutACharacter_DoesNothing()
    {
        var stranger = _fixture.Sessions.GetOrCreate(new Moongate.Tests.TestSupport.Network.ControlledNetworkConnection(77));

        new WarModeRequestPacketHandler(_fixture.Mobiles, _state).Handle(
            stranger,
            new WarModeRequestPacket { WarMode = true }
        );

        Assert.Empty(_state.Flags);
    }

    [Theory]
    [InlineData("7201003200", true)]
    [InlineData("7200003200", false)]
    public void TryParse_ReadsTheModeAsked(string hex, bool warMode)
    {
        Assert.True(WarModeRequestPacket.TryParse(Convert.FromHexString(hex), out var packet));

        Assert.Equal(warMode, packet.WarMode);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
