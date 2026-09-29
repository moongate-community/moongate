using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A text command such as using a skill or casting a spell. (0x12, variable). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x12, PacketSizing.Variable, MinimumLength = 3, Description = "Text command")]
public sealed class TextCommandPacket : BasePacket<TextCommandPacket>, IIncomingPacket<TextCommandPacket>
{
    public override int Length { get; }

    private TextCommandPacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out TextCommandPacket? packet)
    {
        packet = HasValidHeader(data) ? new TextCommandPacket(data.Length) : null;

        return packet is not null;
    }
}
