using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Login denied (0x82): rejects the login with a protocol reason code.</summary>
public readonly record struct LoginDeniedPacket(LoginDeniedReasonType Reason)
{
    public const byte PacketId = 0x82;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)Reason);
    }
}
