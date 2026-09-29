using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to open the chat window. (0xB5, 64 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xB5, PacketSizing.Fixed, Length = 64, Description = "Open chat window")]
public sealed class OpenChatWindowPacket : BaseFixedPacket<OpenChatWindowPacket>, IIncomingPacket<OpenChatWindowPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out OpenChatWindowPacket? packet)
    {
        packet = HasValidHeader(data) ? new OpenChatWindowPacket() : null;

        return packet is not null;
    }
}
