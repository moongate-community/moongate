using Moongate.Network.Packets.Outgoing.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Utils;

public static class TeleportEffectUtils
{
    public const ushort DefaultParticleItemId = 0x3728;
    public const ushort SourceEffectId = 2023;
    public const ushort DestinationEffectId = 5023;
    public const ushort DefaultSoundId = 0x01FE;
    public const byte DefaultSpeed = 10;
    public const byte DefaultDuration = 10;
    public const byte DefaultLayer = 0xFF;

    public static ParticleEffectPacket CreateDestinationEffect(Point3D location)
        => CreateEffect(location, DestinationEffectId);

    public static PlaySoundEffectPacket CreateDestinationSound(Point3D location)
        => new(0x01, DefaultSoundId, 0, location);

    public static ParticleEffectPacket CreateSourceEffect(Point3D location)
        => CreateEffect(location, SourceEffectId);

    private static ParticleEffectPacket CreateEffect(Point3D location, ushort effectId)
        => EffectsFactory.CreateParticle(
            EffectDirectionType.StayAtLocation,
            DefaultParticleItemId,
            Serial.Zero,
            Serial.Zero,
            location,
            location,
            DefaultSpeed,
            DefaultDuration,
            true,
            false,
            0,
            0,
            effectId,
            0,
            0,
            Serial.Zero,
            DefaultLayer,
            0
        );
}
