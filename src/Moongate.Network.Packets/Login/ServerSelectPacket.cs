using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class ServerSelectPacket : IIncomingPacket<ServerSelectPacket>
{
    private const byte PacketOpCode = 0xA0;
    private const int PacketLength = 3;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
    public ushort ServerIndex { get; }

    public ServerSelectPacket(ushort serverIndex)
    {
        ServerIndex = serverIndex;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ServerSelectPacket? packet)
    {
        packet = null;
        if (!PacketValidation.HasFixedHeader(data, PacketOpCode, PacketLength))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt16BigEndian(out var serverIndex))
        {
            return false;
        }

        packet = new ServerSelectPacket(serverIndex);
        return true;
    }
}
