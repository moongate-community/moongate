using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A protocol extension request of the client, known as the Krrios packet. (0xF0, variable). ClassicUO sends it
///     every few seconds while the world map is open, to ask where the members of the party (command 0) and of the
///     guild (command 1) are. Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xF0, PacketSizing.Variable, MinimumLength = 4, Description = "Protocol extension request")]
public sealed class ProtocolExtensionPacket : BasePacket<ProtocolExtensionPacket>, IIncomingPacket<ProtocolExtensionPacket>
{
    public override int Length { get; }

    private ProtocolExtensionPacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ProtocolExtensionPacket? packet)
    {
        packet = HasValidHeader(data) ? new ProtocolExtensionPacket(data.Length) : null;

        return packet is not null;
    }
}
