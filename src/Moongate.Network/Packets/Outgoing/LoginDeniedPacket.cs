using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Login denied (0x82): rejects the login with a protocol reason code.</summary>
[PacketDocumentation(PacketFamilyType.LoginShardSelect, Length = 2)]
public readonly record struct LoginDeniedPacket(LoginDeniedReasonType Reason) : IOutgoingPacket
{
    public const byte PacketId = 0x82;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)Reason);
    }
}
