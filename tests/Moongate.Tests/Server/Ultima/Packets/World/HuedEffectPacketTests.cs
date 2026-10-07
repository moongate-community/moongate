using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Effects;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class HuedEffectPacketTests
{
    [Fact]
    public void Encode_AnEffectAtALocation_IsThirtySixBytesAsModernUO()
    {
        // As ModernUO's CreateLocationHuedEffect: kind 2, no serials, the point twice, speed, duration, two zero
        // bytes, fixed direction 1, explode 0, hue and render mode as 32 bits.
        var effect = new GraphicEffect
        {
            Kind = EffectKindType.FixedLocation, Graphic = (int)EffectGraphicType.Smoke,
            From = new Point3D(1600, 1628, 5), To = new Point3D(1600, 1628, 5), Speed = 10, Duration = 10,
            FixedDirection = true
        };

        Assert.Equal(
            Convert.FromHexString(
                "C0" + "02" + "00000000" + "00000000" + "3728" + "0640" + "065C" + "05" + "0640" + "065C" + "05" + "0A" +
                "0A" +
                "0000" + "01" + "00" + "00000000" + "00000000"
            ),
            PacketCodec.Encode(new HuedEffectPacket(effect))
        );
    }

    [Fact]
    public void Encode_AMovingEffect_WritesBothEndsTheFlagsTheHueAndTheRenderMode()
    {
        var effect = new GraphicEffect
        {
            Kind = EffectKindType.Moving, Source = new Serial(0x00000002), Target = new Serial(0x000088E5),
            Graphic = (int)EffectGraphicType.LargeFireball, From = new Point3D(100, 200, -3),
            To = new Point3D(110, 205, 20), Speed = 7, Duration = 0, Explodes = true, Hue = new Hue(0x047F),
            RenderMode = EffectRenderModeType.Translucent
        };

        Assert.Equal(
            Convert.FromHexString(
                "C0" + "00" + "00000002" + "000088E5" + "36D4" + "0064" + "00C8" + "FD" + "006E" + "00CD" + "14" + "07" +
                "00" +
                "0000" + "00" + "01" + "0000047F" + "00000004"
            ),
            PacketCodec.Encode(new HuedEffectPacket(effect))
        );
    }
}
