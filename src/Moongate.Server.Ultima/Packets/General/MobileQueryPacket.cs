using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request for a mobile's status or for the character's skills (0x34, 10 bytes): four fixed bytes, what is asked
///     and the mobile's serial.
/// </summary>
[PacketHandler(0x34, PacketSizing.Fixed, Length = 10, Description = "Mobile query")]
public sealed class MobileQueryPacket : BaseFixedPacket<MobileQueryPacket>, IIncomingPacket<MobileQueryPacket>
{
    /// <summary>
    ///     Gets what is asked; a value the server does not know is kept as it came.
    /// </summary>
    public MobileQueryType Kind { get; init; }

    public Serial Target { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MobileQueryPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        // 0xEDEDEDED, then the kind and the serial.
        var reader = new PacketReader(data[5..]);

        if (!reader.TryReadByte(out var kind) || !reader.TryReadUInt32BigEndian(out var target))
        {
            return false;
        }

        packet = new() { Kind = (MobileQueryType)kind, Target = new(target) };

        return true;
    }
}
