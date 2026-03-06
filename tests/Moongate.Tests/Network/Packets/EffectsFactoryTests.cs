using Moongate.Network.Packets.Outgoing.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Network.Packets;

public class EffectsFactoryTests
{
    [Test]
    public void CreateBoltEffect_ShouldBuildHuedEffectPacketWithExpectedDefaults()
    {
        var location = new Point3D(200, 300, 15);

        var packet = EffectsFactory.CreateBoltEffect((Serial)0x20u, location, 0x12345678);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.LightningStrike));
                Assert.That(packet.ItemId, Is.EqualTo(0));
                Assert.That(packet.Hue, Is.EqualTo(0x12345678));
                Assert.That(packet.RenderMode, Is.EqualTo(0));
                Assert.That(packet.FixedDirection, Is.False);
                Assert.That(packet.Explode, Is.False);
            }
        );
    }

    [Test]
    public void CreateLightningStrike_ShouldTargetSourceLocation()
    {
        var location = new Point3D(1200, 1300, 10);

        var packet = EffectsFactory.CreateLightningStrike((Serial)0x10u, location);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.LightningStrike));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)0x10u));
                Assert.That(packet.TargetId, Is.EqualTo(Serial.Zero));
                Assert.That(packet.SourceLocation, Is.EqualTo(location));
                Assert.That(packet.TargetLocation, Is.EqualTo(location));
            }
        );
    }

    [Test]
    public void CreateMoving_ShouldBuildGraphicalEffectPacket()
    {
        var source = new Point3D(10, 20, 5);
        var target = new Point3D(30, 40, 0);

        var packet = EffectsFactory.CreateMoving(
            EffectsUtils.Fireball,
            (Serial)1u,
            (Serial)2u,
            source,
            target
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.SourceToTarget));
                Assert.That(packet.ItemId, Is.EqualTo(EffectsUtils.Fireball));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)1u));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)2u));
                Assert.That(packet.SourceLocation, Is.EqualTo(source));
                Assert.That(packet.TargetLocation, Is.EqualTo(target));
                Assert.That(packet.AdjustDirectionDuringAnimation, Is.True);
            }
        );
    }

    [Test]
    public void CreateMovingParticle_ShouldBuildParticleEffectPacket()
    {
        var source = new Point3D(100, 110, 0);
        var target = new Point3D(120, 130, 5);

        var packet = EffectsFactory.CreateMovingParticle(
            EffectsUtils.EnergyBolt,
            (Serial)0x30u,
            (Serial)0x40u,
            source,
            target,
            hue: 0x10203040,
            renderMode: 0x50607080,
            effect: 0x1234,
            explodeEffect: 0x5678,
            explodeSound: 0x9ABC
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.SourceToTarget));
                Assert.That(packet.ItemId, Is.EqualTo(EffectsUtils.EnergyBolt));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)0x30u));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x40u));
                Assert.That(packet.SourceLocation, Is.EqualTo(source));
                Assert.That(packet.TargetLocation, Is.EqualTo(target));
                Assert.That(packet.Hue, Is.EqualTo(0x10203040));
                Assert.That(packet.RenderMode, Is.EqualTo(0x50607080));
                Assert.That(packet.Effect, Is.EqualTo(0x1234));
                Assert.That(packet.ExplodeEffect, Is.EqualTo(0x5678));
                Assert.That(packet.ExplodeSound, Is.EqualTo(0x9ABC));
            }
        );
    }
}
