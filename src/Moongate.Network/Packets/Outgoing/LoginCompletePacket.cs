using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Login complete (0x55): the "you are now in the world" marker that unblocks the client. 1 byte.
/// </summary>
public readonly record struct LoginCompletePacket : IOutgoingPacket
{
    public const byte PacketId = 0x55;

    public void Write(ref SpanWriter writer)
        => writer.Write(PacketId);
}
