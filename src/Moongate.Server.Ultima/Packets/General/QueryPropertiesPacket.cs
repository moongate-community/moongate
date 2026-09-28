using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request for the property lists of objects. (0xD6, variable). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xD6, PacketSizing.Variable, MinimumLength = 3, Description = "Query properties")]
public sealed class QueryPropertiesPacket : BasePacket<QueryPropertiesPacket>, IIncomingPacket<QueryPropertiesPacket>
{
    public override int Length { get; }

    private QueryPropertiesPacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out QueryPropertiesPacket? packet)
    {
        packet = HasValidHeader(data) ? new QueryPropertiesPacket(data.Length) : null;

        return packet is not null;
    }
}
