using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The client asks for the tooltips of several objects (0xD6): their serials. As ModernUO, a length that is not
///     whole serials is refused; as Source-X, more than 500 serials too.
/// </summary>
[PacketHandler(0xD6, PacketSizing.Variable, MinimumLength = 3, Description = "Query properties")]
public sealed class QueryPropertiesPacket : BasePacket<QueryPropertiesPacket>, IIncomingPacket<QueryPropertiesPacket>
{
    public const int MaxSerials = 500;

    private const int HeaderLength = 3;

    public override int Length { get; }

    public IReadOnlyList<Serial> Serials { get; }

    private QueryPropertiesPacket(int length, IReadOnlyList<Serial> serials)
    {
        Length = length;
        Serials = serials;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out QueryPropertiesPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data) || (data.Length - HeaderLength) % 4 != 0 || (data.Length - HeaderLength) / 4 > MaxSerials)
        {
            return false;
        }

        var reader = new PacketReader(data[HeaderLength..]);
        var serials = new List<Serial>();

        while (reader.Remaining > 0 && reader.TryReadUInt32BigEndian(out var serial))
        {
            serials.Add(new(serial));
        }

        packet = new(data.Length, serials);

        return true;
    }
}
