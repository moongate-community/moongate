using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Effects;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class ParticleEffectPacketTests
{
    [Fact]
    public void Encode_IsTheHuedEffectFollowedByTheParticleFields()
    {
        // As ModernUO's CreateParticleEffect: the 0xC0 body, then the particle id, the explode particle, the explode
        // sound, the serial the particles belong to, the layer and two zero bytes: 49 bytes.
        var effect = new GraphicEffect
        {
            Kind = EffectKindType.FixedObject, Source = new Serial(0x00000002), Graphic = (int)EffectGraphicType.SparkleHeal,
            From = new Point3D(1600, 1628, 5), To = new Point3D(1600, 1628, 5), Speed = 9, Duration = 32,
            FixedDirection = true, Particle = 5005, ExplodeParticle = 1, ExplodeSound = 0x160,
            ParticleSerial = new Serial(0x00000002), Layer = EffectLayerType.Waist
        };

        var bytes = PacketCodec.Encode(new ParticleEffectPacket(effect));

        Assert.Equal(49, bytes.Length);
        Assert.Equal(0xC7, bytes[0]);
        Assert.Equal(PacketCodec.Encode(new HuedEffectPacket(effect))[1..], bytes[1..36]);
        Assert.Equal(Convert.FromHexString("138D" + "0001" + "0160" + "00000002" + "03" + "0000"), bytes[36..]);
    }

    [Fact]
    public void Encode_WithoutALayer_Writes255()
    {
        var bytes = PacketCodec.Encode(new ParticleEffectPacket(new GraphicEffect { Particle = 5005 }));

        Assert.Equal(0xFF, bytes[46]);
    }
}
