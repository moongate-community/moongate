using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Incoming.Login;

[PacketHandler(0xA0, PacketSizing.Fixed, Length = 3)]
public sealed class ServerSelectPacket : BaseFixedPacket<ServerSelectPacket>, IIncomingPacket<ServerSelectPacket>
{
    public ushort ServerIndex { get; }

    public ServerSelectPacket(ushort serverIndex)
    {
        ServerIndex = serverIndex;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ServerSelectPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt16BigEndian(out var serverIndex))
        {
            return false;
        }

        packet = new(serverIndex);

        return true;
    }
}
