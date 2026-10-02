using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Effects;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Plays a graphic effect with a hue and a render mode (0xC0, 36 bytes), as ModernUO's hued effect: a moving
///     effect, a lightning bolt, or an animation that stays at a point or on an object.
/// </summary>
[PacketHandler(0xC0, PacketSizing.Fixed, Length = 36)]
public sealed class HuedEffectPacket : BaseFixedPacket<HuedEffectPacket>, IOutgoingPacket
{
    public GraphicEffect Effect { get; }

    public HuedEffectPacket(GraphicEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        Effect = effect;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        WriteBody(ref writer, Effect);
    }

    /// <summary>
    ///     Writes the 35 bytes after the opcode, which the particle effect (0xC7) starts with too.
    /// </summary>
    internal static void WriteBody(ref PacketWriter writer, GraphicEffect effect)
    {
        writer.WriteByte((byte)effect.Kind);
        writer.WriteSerial(effect.Source);
        writer.WriteSerial(effect.Target);
        writer.WriteUInt16BigEndian((ushort)effect.Graphic);
        writer.WriteUInt16BigEndian((ushort)effect.From.X);
        writer.WriteUInt16BigEndian((ushort)effect.From.Y);
        writer.WriteByte(unchecked((byte)(sbyte)effect.From.Z));
        writer.WriteUInt16BigEndian((ushort)effect.To.X);
        writer.WriteUInt16BigEndian((ushort)effect.To.Y);
        writer.WriteByte(unchecked((byte)(sbyte)effect.To.Z));
        writer.WriteByte(effect.Speed);
        writer.WriteByte(effect.Duration);
        writer.WriteUInt16BigEndian(0);
        writer.WriteByte(effect.FixedDirection ? (byte)1 : (byte)0);
        writer.WriteByte(effect.Explodes ? (byte)1 : (byte)0);
        writer.WriteUInt32BigEndian(effect.Hue.Value);
        writer.WriteUInt32BigEndian((uint)effect.RenderMode);
    }
}
