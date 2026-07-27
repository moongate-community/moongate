using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Hues;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// World item (0xF3): draws an item lying in the world. 26 bytes — the modern client's form, which adds
/// a trailing short to the 24-byte one. This packet can carry mobiles and multis too; we only send
/// items, so the entity type is fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers, Length = 24)]
public readonly record struct WorldItemPacket(
    Serial Serial,
    ushort ItemId,
    ushort Amount,
    Point3D Position,
    Hue Hue
) : IOutgoingPacket
{
    public const byte PacketId = 0xF3;

    private const short Command = 0x01;
    private const byte ItemEntity = 0x00; // 1 = mobile, 2 = multi, 3 = damageable — we send items only
    private const short ModernTrailer = 0x00;

    // The client packs the coordinates into a bitfield; the top bits carry other meaning.
    private const int XMask = 0x7FFF;
    private const int YMask = 0x3FFF;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Command);
        writer.Write(ItemEntity);
        writer.Write(Serial);
        writer.Write(ItemId);
        writer.Write((byte)0); // facing, only meaningful for mobiles
        writer.Write(Amount);  // min
        writer.Write(Amount);  // max
        writer.Write((short)(Position.X & XMask));
        writer.Write((short)(Position.Y & YMask));
        writer.Write((sbyte)Position.Z);
        writer.Write((byte)0); // light level: we do not model item light sources
        writer.Write(Hue);
        writer.Write((byte)0); // flags: we do not model movable/hidden yet
        writer.Write(ModernTrailer);
    }
}
