using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to attack a mobile (0x05, 5 bytes): the serial of the target.
/// </summary>
[PacketHandler(0x05, PacketSizing.Fixed, Length = 5, Description = "Attack request")]
public sealed class AttackRequestPacket : BaseFixedPacket<AttackRequestPacket>, IIncomingPacket<AttackRequestPacket>
{
    public required Serial Target { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out AttackRequestPacket? packet)
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

        packet = new() { Target = new Serial(target) };

        return true;
    }
}
