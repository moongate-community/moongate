using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     Whether the contents of a public house are shown. (0xFB, 2 bytes). Only its frame is read: the server does not act on it
///     yet.
/// </summary>
[PacketHandler(0xFB, PacketSizing.Fixed, Length = 2, Description = "Public house content")]
public sealed class PublicHouseContentPacket
    : BaseFixedPacket<PublicHouseContentPacket>, IIncomingPacket<PublicHouseContentPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out PublicHouseContentPacket? packet)
    {
        packet = HasValidHeader(data) ? new PublicHouseContentPacket() : null;

        return packet is not null;
    }
}
