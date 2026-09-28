using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Movement;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Movement;

public sealed class MoveRequestPacketHandlerTests : IAsyncDisposable
{
    private readonly ManualTimeProvider _time = new();
    private readonly StubMovementService _movement = new() { LandingZ = 10 };
    private readonly StubPacketSendService _sender = new();
    private readonly MobileService _mobiles;
    private readonly MobileEntity _aria = new()
    {
        Id = new(2), AccountId = new Serial(42), Name = "Aria", Map = MapType.Trammel,
        Location = new Point3D(1496, 1628, 10), Direction = DirectionType.East
    };

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public MoveRequestPacketHandlerTests()
    {
        _mobiles = new(_movement);
    }

    [Fact]
    public async Task Handle_AStepInTheFacedDirection_MovesAndAcknowledges()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 0);

        Assert.Equal(new Point3D(1497, 1628, 10), _aria.Location);
        var ack = Assert.IsType<MovementAckPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(((byte)0, NotorietyType.Innocent), (ack.Sequence, ack.Notoriety));
    }

    [Fact]
    public async Task Handle_TheNextStep_ExpectsTheNextSequence()
    {
        await EnterAsync();
        await StepAsync(DirectionType.East, 0);
        _time.Advance(TimeSpan.FromMilliseconds(400));

        await StepAsync(DirectionType.East, 1);

        Assert.Equal(new Point3D(1498, 1628, 10), _aria.Location);
        Assert.All(_sender.Sent, packet => Assert.IsType<MovementAckPacket>(packet));
    }

    [Fact]
    public async Task Handle_AWrongSequence_RejectsWithTheRealPositionAndStartsAgainFromZero()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 3);

        var reject = Assert.IsType<MovementRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(((byte)3, new Point3D(1496, 1628, 10), DirectionType.East), (reject.Sequence, reject.Location, reject.Direction));
        Assert.Equal(new Point3D(1496, 1628, 10), _aria.Location);

        await StepAsync(DirectionType.East, 0);

        Assert.IsType<MovementAckPacket>(_sender.Sent[1]);
    }

    [Fact]
    public async Task Handle_AfterSequence255_ExpectsOne()
    {
        await EnterAsync();

        for (var sequence = 0; sequence <= 255; sequence++)
        {
            await StepAsync(DirectionType.East, (byte)sequence);
            _time.Advance(TimeSpan.FromMilliseconds(400));
        }

        await StepAsync(DirectionType.East, 1);

        Assert.All(_sender.Sent, packet => Assert.IsType<MovementAckPacket>(packet));
    }

    [Fact]
    public async Task Handle_ATurn_UsesNoTime_AndTheNextStepGoesThatWay()
    {
        await EnterAsync();
        await StepAsync(DirectionType.East, 0);

        await StepAsync(DirectionType.South, 1);
        Assert.Equal((DirectionType.South, new Point3D(1497, 1628, 10)), (_aria.Direction, _aria.Location));
        // The turn used no time: the step is due 400 ms after the first, inside the credit at 200 ms.
        _time.Advance(TimeSpan.FromMilliseconds(200));
        await StepAsync(DirectionType.South, 2);

        Assert.Equal(DirectionType.South, _aria.Direction);
        Assert.Equal(new Point3D(1497, 1629, 10), _aria.Location);
        Assert.All(_sender.Sent, packet => Assert.IsType<MovementAckPacket>(packet));
    }

    [Fact]
    public async Task Handle_AStepMoreThanTheCreditTooEarly_IsRejected()
    {
        await EnterAsync();
        await StepAsync(DirectionType.East, 0);
        _time.Advance(TimeSpan.FromMilliseconds(199));

        await StepAsync(DirectionType.East, 1);

        Assert.IsType<MovementRejectPacket>(_sender.Sent[1]);
        Assert.Equal(new Point3D(1497, 1628, 10), _aria.Location);
    }

    [Fact]
    public async Task Handle_AStepAtTheEdgeOfTheCredit_IsAccepted()
    {
        await EnterAsync();
        await StepAsync(DirectionType.East, 0);
        _time.Advance(TimeSpan.FromMilliseconds(200));

        await StepAsync(DirectionType.East, 1);

        Assert.IsType<MovementAckPacket>(_sender.Sent[1]);
    }

    [Fact]
    public async Task Handle_Running_AllowsAStepEvery200Milliseconds()
    {
        await EnterAsync();

        for (byte sequence = 0; sequence < 5; sequence++)
        {
            await StepAsync(DirectionType.East, sequence, true);
            _time.Advance(TimeSpan.FromMilliseconds(200));
        }

        Assert.All(_sender.Sent, packet => Assert.IsType<MovementAckPacket>(packet));
        Assert.Equal(new Point3D(1501, 1628, 10), _aria.Location);
    }

    [Fact]
    public async Task Handle_Walking_EveryTwoHundredMilliseconds_RunsOutOfCredit()
    {
        await EnterAsync();

        for (byte sequence = 0; sequence < 3; sequence++)
        {
            await StepAsync(DirectionType.East, sequence);
            _time.Advance(TimeSpan.FromMilliseconds(200));
        }

        Assert.IsType<MovementRejectPacket>(_sender.Sent[2]);
    }

    [Fact]
    public async Task Handle_ABlockedStep_IsRejected()
    {
        await EnterAsync();
        _movement.Allow = false;

        await StepAsync(DirectionType.East, 0);

        Assert.IsType<MovementRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(new Point3D(1496, 1628, 10), _aria.Location);
    }

    [Fact]
    public async Task Handle_WithoutACharacterInTheWorld_SendsNothing()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);

        await StepAsync(DirectionType.East, 0);

        Assert.Empty(_sender.Sent);
    }

    private async Task EnterAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, _aria.Id));
        _mobiles.EnterWorld(_aria);
    }

    private Task StepAsync(DirectionType direction, byte sequence, bool running = false)
    {
        var handler = new MoveRequestPacketHandler(_mobiles, _sender, _time);
        var packet = new MoveRequestPacket { Direction = direction, Running = running, Sequence = sequence, FastWalkKey = 0 };

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, packet));
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
