using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Movement;

/// <summary>
///     Moves the session's character one step (0x02): checks the sequence and the speed, then turns or steps it through
///     <see cref="IMobileService" /> and answers 0x22, or 0x21 with the real position; the players in range see the step or
///     the turn through <see cref="IWorldViewService" />; the scripted items of the new cell are told of the step through
///     <see cref="IMoveOverService" />.
/// </summary>
/// <remarks>
///     Each step books the next one 400 ms later walking, 200 ms running; a step may come up to 200 ms early, which
///     absorbs normal network jitter. A turn uses no time.
/// </remarks>
public sealed class MoveRequestPacketHandler : IPacketHandler<MoveRequestPacket>
{
    private const long WalkDelayMs = 400;
    private const long RunDelayMs = 200;
    private const long CreditMs = 200;
    private const byte LastSequence = 255;

    private readonly ILogger _logger = Log.ForContext<MoveRequestPacketHandler>();
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly IPacketSendService _sender;
    private readonly TimeProvider _time;
    private readonly IBankService? _bank;
    private readonly IMoveOverService? _moveOver;

    public MoveRequestPacketHandler(
        IMobileService mobiles,
        IWorldViewService view,
        IPacketSendService sender,
        TimeProvider time,
        IBankService? bank = null,
        IMoveOverService? moveOver = null
    )
    {
        _bank = bank;
        _moveOver = moveOver;
        _mobiles = mobiles;
        _view = view;
        _sender = sender;
        _time = time;
    }

    public void Handle(GameSession session, MoveRequestPacket packet)
    {
        if (!session.CharacterId.IsValid || !_mobiles.TryGet(session.CharacterId, out var mobile))
        {
            _logger.Debug("Session {SessionId} asked to move without a character in the world", session.SessionId);

            return;
        }

        var state = session.Get(MovementSessionKeys.State);

        if (state is null)
        {
            state = new();
            session.Set(MovementSessionKeys.State, state);
        }

        if (packet.Sequence != state.ExpectedSequence)
        {
            Reject(session, state, mobile, packet.Sequence);

            return;
        }

        var oldLocation = mobile.Location;

        if (packet.Direction != mobile.Direction)
        {
            _mobiles.TryMove(mobile, packet.Direction);
            Accept(session, state, mobile, packet.Sequence);
            _view.Moved(mobile, oldLocation, packet.Running);

            return;
        }

        var now = NowMs();

        if (now + CreditMs < state.NextStepAt || _mobiles.TryMove(mobile, packet.Direction) != MoveResultType.Moved)
        {
            Reject(session, state, mobile, packet.Sequence);

            return;
        }

        state.NextStepAt = Math.Max(now, state.NextStepAt) + (packet.Running ? RunDelayMs : WalkDelayMs);
        // As ModernUO, a step closes the bank box.
        _bank?.Close(mobile);
        Accept(session, state, mobile, packet.Sequence);
        _view.Moved(mobile, oldLocation, packet.Running);
        // Last: a teleporter on the new cell moves the character again and restarts its sequence.
        _moveOver?.SteppedOn(mobile);
    }

    private void Accept(GameSession session, MovementState state, MobileEntity mobile, byte sequence)
    {
        state.ExpectedSequence = sequence == LastSequence ? (byte)1 : (byte)(sequence + 1);
        _sender.TrySend(session.SessionId, new MovementAckPacket(sequence, mobile.Notoriety ?? NotorietyType.Innocent));
    }

    private void Reject(GameSession session, MovementState state, MobileEntity mobile, byte sequence)
    {
        state.ExpectedSequence = 0;
        _sender.TrySend(session.SessionId, new MovementRejectPacket(sequence, mobile.Location, mobile.Direction));
    }

    private long NowMs()
    {
        // Int128: a nanosecond timestamp times 1000 overflows a long after about 106 days of uptime.
        return (long)((Int128)_time.GetTimestamp() * 1000 / _time.TimestampFrequency);
    }
}
