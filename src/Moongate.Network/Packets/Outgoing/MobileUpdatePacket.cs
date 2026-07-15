using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using Moongate.UO.Data.Hues;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Draw game player / mobile update (0x20): positions and renders the player's own mobile. 19 bytes fixed.
/// </summary>
public readonly record struct MobileUpdatePacket(
    Serial Serial,
    ushort Body,
    Hue Hue,
    byte Flags,
    ushort X,
    ushort Y,
    sbyte Z,
    DirectionType Direction
) : IOutgoingPacket
{
    public const byte PacketId = 0x20;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(Body);
        writer.Write((byte)0); // unknown
        writer.Write(Hue);
        writer.Write(Flags);
        writer.Write(X);
        writer.Write(Y);
        writer.Write((ushort)0); // unknown
        writer.Write((byte)Direction);
        writer.Write(Z);
    }
}
