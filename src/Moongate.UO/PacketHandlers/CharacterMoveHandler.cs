using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class CharacterMoveHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<CharacterMoveHandler>();

    private readonly IMobileService _mobileService;

    private const long MovementThrottleReset = 1000;    // 1 second throttle reset
    private const long MovementThrottleThreshold = 400; // 400 milliseconds throttle threshold

    private const int TurnDelay = 0;
    private const int WalkFootDelay = 400;
    private const int RunFootDelay = 200;
    private const int WalkMountDelay = 200;
    private const int RunMountDelay = 100;

    public CharacterMoveHandler(IMobileService mobileService)
        => _mobileService = mobileService;

    public static int ComputeSpeed(UOMobileEntity mobile, DirectionType direction)
    {
        if (mobile.IsMounted)
        {
            return (direction & DirectionType.Running) != 0 ? RunMountDelay : WalkMountDelay;
        }

        return (direction & DirectionType.Running) != 0 ? RunFootDelay : WalkFootDelay;
    }

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is MoveRequestPacket moveRequestPacket)
        {
            await ProcessMoveRequestAsync(session, moveRequestPacket);
        }

        if (packet is MoveAckPacket moveAckPacket)
        {
            _logger.Debug(
                "Received MoveAckPacket for session {SessionId} with sequence {MoveSequence}",
                session.SessionId,
                moveAckPacket.Sequence
            );
        }
    }

    public static bool Throttle(GameSession session)
    {
        if (session.Account.AccountLevel > AccountLevelType.User)
        {
            return false; // Admins | GM  are not throttled
        }

        var now = Environment.TickCount64;
        var credit = session.MoveCredit;
        var nextMove = session.MoveTime;

        // Reset system if idle for more than 1 second
        if (now - nextMove + MovementThrottleReset > 0)
        {
            session.MoveCredit = 0;
            session.MoveTime = now;

            return false;
        }

        var cost = nextMove - now;

        if (credit < cost)
        {
            // Not enough credit, therefore throttled
            return true;
        }

        // On the next event loop, the player receives up to 400ms in grace latency
        session.MoveCredit = Math.Min(MovementThrottleThreshold, credit - cost);

        return false;
    }

    private async Task ProcessMoveRequestAsync(GameSession session, MoveRequestPacket packet)
    {
        // Bug fix: return after sequence mismatch rejection
        if (session.MoveSequence == 0 && packet.Sequence != 0)
        {
            session.SendPackets(new MoveRejectionPacket(packet.Sequence, session.Mobile.Location, packet.Direction));
            session.MoveSequence = 0;

            return;
        }

        if (Throttle(session))
        {
            session.SendPackets(new MoveRejectionPacket(packet.Sequence, session.Mobile.Location, packet.Direction));

            return;
        }

        var currentDir = (DirectionType)((byte)session.Mobile.Direction & 0x07);
        var requestedDir = (DirectionType)((byte)packet.Direction & 0x07);

        if (currentDir == requestedDir)
        {
            // Same base direction = actual positional movement
            var newLocation = session.Mobile.Location + packet.Direction;
            var landTile = session.Mobile.Map.GetLandTile(newLocation.X, newLocation.Y);
            newLocation = new(newLocation.X, newLocation.Y, landTile.Z);

            _mobileService.MoveMobile(session.Mobile, newLocation);
        }

        // Update direction (with Running flag preserved)
        session.Mobile.Direction = packet.Direction;

        var isRunning = (packet.Direction & DirectionType.Running) != 0;

        _logger.Debug(
            "Processing move request for session {SessionId} with sequence {MoveSequence} Direction {Direction} is running {IsRunning}",
            session.SessionId,
            session.MoveSequence,
            packet.Direction,
            isRunning
        );

        session.MoveTime += ComputeSpeed(session.Mobile, packet.Direction);

        // Send ack with server sequence BEFORE incrementing
        var moveAckPacket = new MoveAckPacket(session.Mobile, (byte)session.MoveSequence);

        session.SendPackets(moveAckPacket);

        // Increment sequence AFTER sending ack (matching ModernUO)
        var newSeq = (int)packet.Sequence + 1;

        if (newSeq == 256)
        {
            newSeq = 1;
        }

        session.MoveSequence = (byte)newSeq;
    }
}
