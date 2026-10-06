using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to resend the player's position. (0x22, 3 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x22, PacketSizing.Fixed, Length = 3, Description = "Resynchronize request")]
public sealed class ResynchronizeRequestPacket
    : BaseFixedPacket<ResynchronizeRequestPacket>, IIncomingPacket<ResynchronizeRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ResynchronizeRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new ResynchronizeRequestPacket() : null;

        return packet is not null;
    }
}
