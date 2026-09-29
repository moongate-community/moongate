using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A single click on an object (0x09, 5 bytes): its serial.
/// </summary>
[PacketHandler(0x09, PacketSizing.Fixed, Length = 5, Description = "Look request")]
public sealed class LookRequestPacket : BaseFixedPacket<LookRequestPacket>, IIncomingPacket<LookRequestPacket>
{
    public required Serial Target { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out LookRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var target))
        {
            return false;
        }

        packet = new() { Target = new(target) };

        return true;
    }
}
