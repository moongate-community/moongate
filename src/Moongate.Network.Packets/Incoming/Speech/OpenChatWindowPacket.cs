using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Speech;

[PacketHandler(0xB5, PacketSizing.Fixed, Length = 64, Description = "Open Chat Window")]

/// <summary>
/// Represents OpenChatWindowPacket.
/// </summary>
public class OpenChatWindowPacket : BaseGameNetworkPacket
{
    /// <summary>
    /// Raw 63-byte payload sent by client.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; private set; } = ReadOnlyMemory<byte>.Empty;

    public OpenChatWindowPacket()
        : base(0xB5, 64) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 63)
        {
            return false;
        }

        Payload = reader.ReadBytes(63).ToArray();

        return reader.Remaining == 0;
    }
}
