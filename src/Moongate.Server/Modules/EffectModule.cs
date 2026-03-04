using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("effect", "Provides visual effect helpers for scripts.")]
public sealed class EffectModule
{
    private readonly IDispatchEventsService _dispatchEventsService;

    public EffectModule(IDispatchEventsService dispatchEventsService)
    {
        _dispatchEventsService = dispatchEventsService;
    }

    [ScriptFunction("send", "Broadcasts a location effect to players in range.")]
    public int Send(
        int mapId,
        int x,
        int y,
        int z,
        int itemId,
        int speed = 10,
        int duration = 10,
        int hue = 0,
        int renderMode = 0,
        int effect = 0,
        int explodeEffect = 0,
        int explodeSound = 0,
        int layer = 0xFF,
        int unknown3 = 0
    )
    {
        if (mapId < 0 || itemId < 0)
        {
            return 0;
        }

        return _dispatchEventsService.DispatchMobileEffectAsync(
            mapId,
            new Point3D(x, y, z),
            (ushort)Math.Min(itemId, ushort.MaxValue),
            (byte)Math.Clamp(speed, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(duration, byte.MinValue, byte.MaxValue),
            hue,
            renderMode,
            (ushort)Math.Clamp(effect, ushort.MinValue, ushort.MaxValue),
            (ushort)Math.Clamp(explodeEffect, ushort.MinValue, ushort.MaxValue),
            (ushort)Math.Clamp(explodeSound, ushort.MinValue, ushort.MaxValue),
            (byte)Math.Clamp(layer, byte.MinValue, byte.MaxValue),
            (ushort)Math.Clamp(unknown3, ushort.MinValue, ushort.MaxValue)
        ).GetAwaiter().GetResult();
    }

    [ScriptFunction("send_to_player", "Sends a location effect to a single character id.")]
    public bool SendToPlayer(
        uint characterId,
        int x,
        int y,
        int z,
        int itemId,
        int speed = 10,
        int duration = 10,
        int hue = 0,
        int renderMode = 0,
        int effect = 0,
        int explodeEffect = 0,
        int explodeSound = 0,
        int layer = 0xFF,
        int unknown3 = 0
    )
        => SetEffectToPlayer(
            characterId,
            x,
            y,
            z,
            itemId,
            speed,
            duration,
            hue,
            renderMode,
            effect,
            explodeEffect,
            explodeSound,
            layer,
            unknown3
        );

    public bool SetEffectToPlayer(
        uint characterId,
        int x,
        int y,
        int z,
        int itemId,
        int speed = 10,
        int duration = 10,
        int hue = 0,
        int renderMode = 0,
        int effect = 0,
        int explodeEffect = 0,
        int explodeSound = 0,
        int layer = 0xFF,
        int unknown3 = 0
    )
    {
        if (characterId == 0 || itemId < 0)
        {
            return false;
        }

        return _dispatchEventsService.DispatchEffectToPlayerAsync(
            (Serial)characterId,
            new Point3D(x, y, z),
            (ushort)Math.Min(itemId, ushort.MaxValue),
            (byte)Math.Clamp(speed, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(duration, byte.MinValue, byte.MaxValue),
            hue,
            renderMode,
            (ushort)Math.Clamp(effect, ushort.MinValue, ushort.MaxValue),
            (ushort)Math.Clamp(explodeEffect, ushort.MinValue, ushort.MaxValue),
            (ushort)Math.Clamp(explodeSound, ushort.MinValue, ushort.MaxValue),
            (byte)Math.Clamp(layer, byte.MinValue, byte.MaxValue),
            (ushort)Math.Clamp(unknown3, ushort.MinValue, ushort.MaxValue)
        ).GetAwaiter().GetResult();
    }
}
