using Moongate.Tests.TestSupport.Ultima.Bank;
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
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Movement;

public sealed class MoveRequestPacketHandlerTests : IAsyncDisposable
{
    private readonly ManualTimeProvider _time = new();
    private readonly StubBankService _bank = new();
    private readonly RecordingMoveOverService _moveOver = new();
    private readonly RecordingFatigueService _fatigue = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubMovementService _movement = new() { LandingZ = 10 };
    private readonly StubPacketSendService _sender = new();
    private readonly RecordingWorldViewService _view = new();
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
        _mobiles = new(_movement, TestSectors.Create());
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
    public async Task Handle_AStep_CostsItsStamina_ATurnAsksNothing()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 0, true);
        await StepAsync(DirectionType.South, 1);

        // The run was asked about and then paid; the turn neither.
        Assert.Equal([true], _fatigue.Asked);
        Assert.Equal([true], _fatigue.Taken);
    }

    [Fact]
    public async Task Handle_AStepThePlayerIsTooTiredFor_IsRefused_AndCostsNothing()
    {
        await EnterAsync();
        _fatigue.Allows = false;

        await StepAsync(DirectionType.East, 0);

        Assert.IsType<MovementRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(new Point3D(1496, 1628, 10), _aria.Location);
        Assert.Empty(_fatigue.Taken);
    }

    [Fact]
    public async Task Handle_AStepTheWorldRefuses_CostsNothing()
    {
        await EnterAsync();
        _aria.Frozen = true;

        await StepAsync(DirectionType.East, 0);

        Assert.Empty(_fatigue.Taken);
    }

    [Fact]
    public async Task Handle_AStep_ClosesTheBank_ATurnDoesNot()
    {
        await EnterAsync();
        await StepAsync(DirectionType.East, 0);
        await StepAsync(DirectionType.South, 1);

        Assert.Equal([_aria], _bank.Closed);
    }

    [Fact]
    public async Task Handle_AStep_TellsTheItemsOfTheNewCell_AfterTheAckAndThePlayersAround()
    {
        await EnterAsync();
        _moveOver.OnCall = () =>
        {
            Assert.IsType<MovementAckPacket>(Assert.Single(_sender.Sent));
            Assert.Single(_view.Calls, call => call.StartsWith("Moved 2", StringComparison.Ordinal));
        };

        await StepAsync(DirectionType.East, 0);

        Assert.Equal([new Point3D(1497, 1628, 10)], _moveOver.Steps);
    }

    [Fact]
    public async Task Handle_ATurnOrARefusedStep_TellsNoItem()
    {
        await EnterAsync();

        await StepAsync(DirectionType.South, 0);
        await StepAsync(DirectionType.South, 9);

        Assert.Empty(_moveOver.Steps);
    }

    [Theory]
    [InlineData(AccountType.Regular, MovementAbilityType.Walk)]
    [InlineData(AccountType.GameMaster, MovementAbilityType.Walk | MovementAbilityType.PassDoors)]
    [InlineData(AccountType.Administrator, MovementAbilityType.Walk | MovementAbilityType.PassDoors)]
    public async Task Handle_AStep_OnlyStaffWalksThroughDoors(AccountType account, MovementAbilityType expected)
    {
        await EnterAsync();
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, account));

        await StepAsync(DirectionType.East, 0);

        Assert.Equal(expected, Assert.Single(_movement.Abilities));
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

    [Fact]
    public async Task Handle_AnAcceptedStep_TellsTheWorldViewTheOldLocation()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 0);

        Assert.Equal(["Moved 2 1496,1628,10"], _view.Calls);
    }

    [Fact]
    public async Task Handle_ATurn_TellsTheWorldView()
    {
        await EnterAsync();

        await StepAsync(DirectionType.North, 0);

        Assert.Equal(["Moved 2 1496,1628,10"], _view.Calls);
    }

    [Fact]
    public async Task Handle_ARunningStep_TellsTheWorldViewItRuns()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 0, true);

        Assert.Equal(["Moved 2 1496,1628,10 run"], _view.Calls);
    }

    [Fact]
    public async Task Handle_ARejectedStep_TellsTheWorldViewNothing()
    {
        await EnterAsync();
        _movement.Allow = false;

        await StepAsync(DirectionType.East, 0);
        await StepAsync(DirectionType.East, 7);

        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_AStepTooSoon_TellsTheWorldViewOnlyTheFirst()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 0);
        await StepAsync(DirectionType.East, 1);

        Assert.Equal(["Moved 2 1496,1628,10"], _view.Calls);
    }

    [Fact]
    public async Task Handle_ATurnOfAFrozenCharacter_IsRejectedAndTellsTheWorldViewNothing()
    {
        await EnterAsync();
        _aria.Frozen = true;
        var facing = _aria.Direction;

        await StepAsync(DirectionType.West, 0);

        Assert.IsType<MovementRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(facing, _aria.Direction);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_AStepOfAFrozenCharacter_IsRejected()
    {
        await EnterAsync();
        _aria.Frozen = true;

        await StepAsync(_aria.Direction, 0);

        Assert.IsType<MovementRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Equal(new Point3D(1496, 1628, 10), _aria.Location);
    }

    [Fact]
    public async Task Handle_AStep_NotesWhenTheMobileMoved_ATurnDoesNot()
    {
        await EnterAsync();
        Assert.Null(_aria.LastMovedAt);

        // A turn: the mobile faces another way and stays where it is.
        await StepAsync(DirectionType.North, 0);
        Assert.Null(_aria.LastMovedAt);

        await StepAsync(DirectionType.North, 1);

        Assert.InRange((_time.GetUtcNow() - _aria.LastMovedAt!.Value).TotalSeconds, 0, 1);
    }

    [Fact]
    public async Task Handle_AStepOfAHiddenPlayer_ShowsIt_AndSaysSo()
    {
        await EnterAsync();
        _aria.Hidden = true;

        await StepAsync(DirectionType.East, 0);

        Assert.False(_aria.Hidden);
        Assert.Equal([(_aria, MoveRequestPacketHandler.RevealedCliloc, "")], _speech.ToldClilocs);
    }

    [Fact]
    public async Task Handle_ATurnOrARefusedStepOfAHiddenPlayer_KeepsItHidden()
    {
        await EnterAsync();
        _aria.Hidden = true;

        await StepAsync(DirectionType.South, 0);
        _aria.Frozen = true;
        await StepAsync(DirectionType.South, 1);

        Assert.True(_aria.Hidden);
        Assert.Empty(_speech.ToldClilocs);
    }

    [Fact]
    public async Task Handle_AStepOfAPlayerInSight_SaysNothing()
    {
        await EnterAsync();

        await StepAsync(DirectionType.East, 0);

        Assert.Empty(_speech.ToldClilocs);
    }

    [Fact]
    public async Task Handle_AStepOfAGhost_KeepsItHidden()
    {
        await EnterAsync();
        _aria.AccountId = new Serial(0x42);
        _aria.Body = 0x0192;
        _aria.Hidden = true;

        await StepAsync(DirectionType.East, 0);

        Assert.True(_aria.Hidden);
        Assert.Empty(_speech.ToldClilocs);
    }

    [Theory]
    [InlineData(AccountType.GameMaster)]
    [InlineData(AccountType.Administrator)]
    public async Task Handle_AStepOfHiddenStaff_KeepsItHidden(AccountType account)
    {
        await EnterAsync();
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, account));
        _aria.Hidden = true;

        await StepAsync(DirectionType.East, 0);

        Assert.True(_aria.Hidden);
        Assert.Empty(_speech.ToldClilocs);
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
        var handler = new MoveRequestPacketHandler(_mobiles, _view, _sender, _time, _bank, _moveOver, _fatigue, _state, _speech);
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
