using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.World;

/// <summary>
/// Creates commonly used visual effect packets with protocol-safe defaults.
/// </summary>
public static class EffectsFactory
{
    public static HuedEffectPacket CreateBoltEffect(Serial sourceId, Point3D sourceLocation, int hue = 0)
        => CreateHued(
            EffectDirectionType.LightningStrike,
            0,
            sourceId,
            Serial.Zero,
            sourceLocation,
            sourceLocation,
            0,
            0,
            false,
            false,
            hue,
            0
        );

    public static GraphicalEffectPacket CreateGraphical(
        EffectDirectionType directionType,
        ushort itemId,
        Serial sourceId,
        Serial targetId,
        Point3D sourceLocation,
        Point3D targetLocation,
        int speed = 5,
        int duration = 5,
        int unknown2 = 0,
        bool adjustDirectionDuringAnimation = true,
        bool explodeOnImpact = false
    )
        => new(
            directionType,
            sourceId,
            targetId,
            itemId,
            sourceLocation,
            targetLocation,
            ToByte(speed),
            ToByte(duration),
            ToUShort(unknown2),
            adjustDirectionDuringAnimation,
            explodeOnImpact
        );

    public static HuedEffectPacket CreateHued(
        EffectDirectionType directionType,
        ushort itemId,
        Serial sourceId,
        Serial targetId,
        Point3D sourceLocation,
        Point3D targetLocation,
        int speed = 5,
        int duration = 5,
        bool fixedDirection = false,
        bool explode = false,
        int hue = 0,
        int renderMode = 0,
        int unknown1 = 0,
        int unknown2 = 0
    )
        => new(
            directionType,
            sourceId,
            targetId,
            itemId,
            sourceLocation,
            targetLocation,
            ToByte(speed),
            ToByte(duration),
            fixedDirection,
            explode,
            hue,
            renderMode,
            ToByte(unknown1),
            ToByte(unknown2)
        );

    public static GraphicalEffectPacket CreateLightningStrike(
        Serial sourceId,
        Point3D sourceLocation,
        ushort itemId = 0,
        int speed = 0,
        int duration = 0
    )
        => CreateGraphical(
            EffectDirectionType.LightningStrike,
            itemId,
            sourceId,
            Serial.Zero,
            sourceLocation,
            sourceLocation,
            speed,
            duration
        );

    public static GraphicalEffectPacket CreateMoving(
        ushort itemId,
        Serial sourceId,
        Serial targetId,
        Point3D sourceLocation,
        Point3D targetLocation,
        int speed = 5,
        int duration = 5,
        bool explodeOnImpact = false
    )
        => CreateGraphical(
            EffectDirectionType.SourceToTarget,
            itemId,
            sourceId,
            targetId,
            sourceLocation,
            targetLocation,
            speed,
            duration,
            0,
            true,
            explodeOnImpact
        );

    public static ParticleEffectPacket CreateMovingParticle(
        ushort itemId,
        Serial sourceId,
        Serial targetId,
        Point3D sourceLocation,
        Point3D targetLocation,
        int speed = 5,
        int duration = 5,
        bool fixedDirection = false,
        bool explode = false,
        int hue = 0,
        int renderMode = 0,
        int effect = 0,
        int explodeEffect = 0,
        int explodeSound = 0,
        Serial effectSerial = default,
        int layer = 0,
        int unknown3 = 0
    )
        => CreateParticle(
            EffectDirectionType.SourceToTarget,
            itemId,
            sourceId,
            targetId,
            sourceLocation,
            targetLocation,
            speed,
            duration,
            fixedDirection,
            explode,
            hue,
            renderMode,
            effect,
            explodeEffect,
            explodeSound,
            effectSerial,
            layer,
            unknown3
        );

    public static ParticleEffectPacket CreateParticle(
        EffectDirectionType directionType,
        ushort itemId,
        Serial sourceId,
        Serial targetId,
        Point3D sourceLocation,
        Point3D targetLocation,
        int speed = 5,
        int duration = 5,
        bool fixedDirection = false,
        bool explode = false,
        int hue = 0,
        int renderMode = 0,
        int effect = 0,
        int explodeEffect = 0,
        int explodeSound = 0,
        Serial effectSerial = default,
        int layer = 0,
        int unknown3 = 0,
        int unknown1 = 0,
        int unknown2 = 0
    )
        => new(
            directionType,
            sourceId,
            targetId,
            itemId,
            sourceLocation,
            targetLocation,
            ToByte(speed),
            ToByte(duration),
            fixedDirection,
            explode,
            hue,
            renderMode,
            ToUShort(effect),
            ToUShort(explodeEffect),
            ToUShort(explodeSound),
            effectSerial,
            ToByte(layer),
            ToUShort(unknown3),
            ToByte(unknown1),
            ToByte(unknown2)
        );

    private static byte ToByte(int value)
        => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);

    private static ushort ToUShort(int value)
        => (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
}
