using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Primitives;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Shows an item on the ground to a client before 7.0.0.0 (0x1A, variable); newer clients get
///     <see cref="WorldItemSaPacket" />. Written as ModernUO does, without direction or flags.
/// </summary>
[PacketHandler(0x1A, PacketSizing.Variable, MinimumLength = 16)]
public sealed class WorldItemPacket : BasePacket<WorldItemPacket>, IOutgoingPacket
{
    private const int FixedLength = 16;
    private const int HueLength = 2;
    private const int LightLength = 1;
    private const uint AmountFlag = 0x80000000; // The serial's high bit: an amount follows.
    private const int ItemIdMask = 0x3FFF;
    private const int XMask = 0x7FFF;
    private const int YMask = 0x3FFF;
    private const int ExtraFlag = 0x8000; // The high bit of X (light) or Y (hue): an extra field follows.

    public override int Length { get; }

    public Serial Serial { get; }

    public int ItemId { get; }

    public int Amount { get; }

    public Point3D Location { get; }

    public Hue Hue { get; }

    /// <summary>
    ///     The shape of the light a light source gives, a LightType value; 0 sends none.
    /// </summary>
    public int Light { get; }

    public WorldItemPacket(Serial serial, int itemId, int amount, Point3D location, Hue hue, int light = 0)
    {
        Light = light;
        Serial = serial;
        ItemId = itemId;
        Amount = amount;
        Location = location;
        Hue = hue;
        Length = FixedLength + (hue.Value != 0 ? HueLength : 0) + (light != 0 ? LightLength : 0);
    }

    public void Write(ref PacketWriter writer)
    {
        var hasHue = Hue.Value != 0;
        var hasLight = Light != 0;

        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        // The high bit says an amount follows; every item has one.
        writer.WriteUInt32BigEndian(Serial.Value | AmountFlag);
        writer.WriteUInt16BigEndian((ushort)(ItemId & ItemIdMask));
        writer.WriteUInt16BigEndian((ushort)Amount);
        // The high bit of X says the light (the direction byte) follows Y.
        writer.WriteUInt16BigEndian((ushort)((Location.X & XMask) | (hasLight ? ExtraFlag : 0)));
        writer.WriteUInt16BigEndian((ushort)((Location.Y & YMask) | (hasHue ? ExtraFlag : 0)));

        if (hasLight)
        {
            writer.WriteByte((byte)Light);
        }

        writer.WriteByte(unchecked((byte)(sbyte)Location.Z));

        if (hasHue)
        {
            writer.WriteUInt16BigEndian(Hue.Value);
        }
    }
}
