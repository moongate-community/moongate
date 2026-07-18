using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Update player (0x77): broadcasts another mobile's position/facing to nearby players.</summary>
[PacketDocumentation(PacketFamilyType.Movement, Length = 17, Name = "Update Player")]
public readonly record struct UpdatePlayerPacket(
    Serial Serial,
    ushort Body,
    ushort X,
    ushort Y,
    sbyte Z,
    DirectionType Direction,
    Hue Hue,
    byte Flags,
    NotorietyType Notoriety
) : IOutgoingPacket
{
    public const byte PacketId = 0x77;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(Body);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Z);
        writer.Write((byte)Direction);
        writer.Write(Hue);
        writer.Write(Flags);
        writer.Write((byte)Notoriety);
    }
}
