using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     What the player says. (0xAD, variable). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xAD, PacketSizing.Variable, MinimumLength = 3, Description = "Unicode speech request")]
public sealed class UnicodeSpeechRequestPacket : BasePacket<UnicodeSpeechRequestPacket>, IIncomingPacket<UnicodeSpeechRequestPacket>
{
    public override int Length { get; }

    private UnicodeSpeechRequestPacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out UnicodeSpeechRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new UnicodeSpeechRequestPacket(data.Length) : null;

        return packet is not null;
    }
}
