using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Login confirmation / character locale and body (0x1B): the first packet of the enter-world burst.
/// Tells the client which mobile it is playing, where it stands, and the facet's dimensions. 37 bytes fixed.
/// </summary>
public readonly record struct LoginConfirmPacket(
    Serial Serial,
    ushort Body,
    ushort X,
    ushort Y,
    short Z,
    DirectionType Direction,
    ushort MapWidth,
    ushort MapHeight
) : IOutgoingPacket
{
    public const byte PacketId = 0x1B;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(0);            // unknown, always 0
        writer.Write(Body);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Z);
        writer.Write((byte)Direction);
        writer.Write((byte)0);      // unknown
        writer.Write(-1);           // unknown, always -1
        writer.Write(0);            // unknown, always 0
        writer.Write(MapWidth);
        writer.Write(MapHeight);
        writer.Write(0);            // padding to 37
        writer.Write((ushort)0);    // padding to 37
    }
}
