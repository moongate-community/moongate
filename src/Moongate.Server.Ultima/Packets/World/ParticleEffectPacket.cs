using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Effects;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Plays a graphic effect with particles (0xC7, 49 bytes), as ModernUO's particle effect: the body of the hued effect
///     (0xC0) followed by the particle fields. Only the Enhanced Client is sent it.
/// </summary>
[PacketHandler(0xC7, PacketSizing.Fixed, Length = 49)]
public sealed class ParticleEffectPacket : BaseFixedPacket<ParticleEffectPacket>, IOutgoingPacket
{
    public GraphicEffect Effect { get; }

    public ParticleEffectPacket(GraphicEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        Effect = effect;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        HuedEffectPacket.WriteBody(ref writer, Effect);
        writer.WriteUInt16BigEndian((ushort)Effect.Particle);
        writer.WriteUInt16BigEndian((ushort)Effect.ExplodeParticle);
        writer.WriteUInt16BigEndian((ushort)Effect.ExplodeSound);
        writer.WriteSerial(Effect.ParticleSerial);
        writer.WriteByte((byte)Effect.Layer);
        writer.WriteUInt16BigEndian(0);
    }
}
