using Moongate.Network.Packets.Incoming.Targeting;

namespace Moongate.Server.Data.Internal.Cursors;

public record PendingCursorCallback(TargetCursorCommandsPacket Packet);
