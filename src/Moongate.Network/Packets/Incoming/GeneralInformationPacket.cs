using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// General information (0xBF): a multiplexed request whose meaning is chosen by a leading
/// <see cref="SubCommand" /> (ushort). Variable length: 5-byte header (id + length + sub-command)
/// followed by the sub-command payload, which is carried verbatim in <see cref="Payload" />.
/// </summary>
public readonly record struct GeneralInformationPacket(ushort SubCommand, byte[] Payload)
    : IIncomingPacket<GeneralInformationPacket>
{
    public static byte PacketId => 0xBF;

    public static GeneralInformationPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();                    // packet id
        var length = reader.ReadUInt16();     // full packet length
        var subCommand = reader.ReadUInt16(); // multiplexed sub-command

        var payloadLength = Math.Max(0, length - 5);
        var payload = payloadLength == 0 ? [] : reader.ReadBytes(payloadLength).ToArray();

        return new(subCommand, payload);
    }
}
