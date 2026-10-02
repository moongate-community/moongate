using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to show or to edit a character's profile. (0xB8, variable). The Enhanced Client sends it for its own
///     character right after entering the world. Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xB8, PacketSizing.Variable, MinimumLength = 8, Description = "Character profile request")]
public sealed class ProfileRequestPacket : BasePacket<ProfileRequestPacket>, IIncomingPacket<ProfileRequestPacket>
{
    public override int Length { get; }

    private ProfileRequestPacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ProfileRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new ProfileRequestPacket(data.Length) : null;

        return packet is not null;
    }
}
