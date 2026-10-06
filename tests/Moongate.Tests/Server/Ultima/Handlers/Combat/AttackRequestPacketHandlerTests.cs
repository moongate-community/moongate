using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Combat;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Handlers.Combat;

public sealed class AttackRequestPacketHandlerTests : IAsyncLifetime
{
    private readonly RecordingCombatService _combat = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _boris = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out _boris!));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Handle_TellsTheCombatServiceToAttackTheTarget()
    {
        Create().Handle(_session, new() { Target = new Serial(3) });

        Assert.Equal([(_aria, _boris)], _combat.Attacks);
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void Handle_ARefusedAttack_ClearsTheTargetOfTheClient()
    {
        _combat.Allows = false;

        Create().Handle(_session, new() { Target = new Serial(3) });

        Assert.Equal(Serial.Zero, Assert.IsType<CombatantPacket>(Assert.Single(_fixture.Sender.Sent)).Target);
    }

    [Fact]
    public void Handle_ATargetThatIsNotInTheWorld_ClearsTheTargetOfTheClient_AndAttacksNoOne()
    {
        Create().Handle(_session, new() { Target = new Serial(999) });

        Assert.Empty(_combat.Attacks);
        Assert.Equal(Serial.Zero, Assert.IsType<CombatantPacket>(Assert.Single(_fixture.Sender.Sent)).Target);
    }

    [Fact]
    public void Handle_WithoutACharacter_DoesNothing()
    {
        var stranger = _fixture.Sessions.GetOrCreate(new ControlledNetworkConnection(77));

        Create().Handle(stranger, new() { Target = new Serial(3) });

        Assert.Empty(_combat.Attacks);
        Assert.Empty(_fixture.Sender.Sent);
    }

    private AttackRequestPacketHandler Create()
    {
        return new(_fixture.Mobiles, _combat, _fixture.Sender);
    }
}
