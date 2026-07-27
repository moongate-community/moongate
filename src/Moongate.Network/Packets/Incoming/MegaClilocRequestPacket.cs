using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Mega cliloc request (0xD6): the client asks for the property lists of a batch of objects,
/// identified by serial. Variable length: a 3-byte header followed by 4-byte serials; a payload
/// that is not a multiple of four yields an empty batch.
/// </summary>
[PacketDocumentation(PacketFamilyType.Tooltips, IsVariableLength = true)]
public readonly record struct MegaClilocRequestPacket(IReadOnlyList<Serial> Serials)
    : IIncomingPacket<MegaClilocRequestPacket>
{
    public static byte PacketId => 0xD6;

    public static MegaClilocRequestPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();                // packet id
        var length = reader.ReadUInt16(); // full packet length
        var payload = length - 3;

        if (payload < 4 || payload % 4 != 0)
        {
            return new([]);
        }

        var serials = new Serial[payload / 4];

        for (var i = 0; i < serials.Length; i++)
        {
            serials[i] = new(reader.ReadUInt32());
        }

        return new(serials);
    }
}
