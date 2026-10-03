using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to switch between peace and war (0x72, 5 bytes): the mode asked, then three bytes the server does not
///     read.
/// </summary>
[PacketHandler(0x72, PacketSizing.Fixed, Length = 5, Description = "War mode request")]
public sealed class WarModeRequestPacket : BaseFixedPacket<WarModeRequestPacket>, IIncomingPacket<WarModeRequestPacket>
{
    public bool WarMode { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out WarModeRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadByte(out var mode))
        {
            return false;
        }

        packet = new() { WarMode = mode != 0 };

        return true;
    }
}
