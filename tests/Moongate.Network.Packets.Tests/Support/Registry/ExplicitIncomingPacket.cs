using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Registry;

[PacketHandler(0xD1, PacketSizing.Fixed, Length = 2)]
public sealed class ExplicitIncomingPacket : IIncomingPacket<ExplicitIncomingPacket>
{
    public byte OpCode => 0xD1;
    public int Length => 2;
    public byte Value { get; }

    private ExplicitIncomingPacket(byte value)
    {
        Value = value;
    }

    static bool IIncomingPacket<ExplicitIncomingPacket>.TryParse(
        ReadOnlySpan<byte> data, [NotNullWhen(true)] out ExplicitIncomingPacket? packet
    )
    {
        packet = data.Length == 2 && data[0] == 0xD1 && data[1] != 0
            ? new ExplicitIncomingPacket(data[1])
            : null;
        return packet is not null;
    }
}
