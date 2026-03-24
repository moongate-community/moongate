using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Modules;

[ScriptModule("combat", "Provides basic combat targeting and swing helpers for Lua brains.")]

/// <summary>
/// Exposes combat primitives for npc brains.
/// </summary>
public sealed class CombatModule
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly ICombatService _combatService;

    public CombatModule(
        ISpatialWorldService spatialWorldService,
        IGameEventBusService gameEventBusService,
        ICombatService combatService
    )
    {
        _spatialWorldService = spatialWorldService;
        _gameEventBusService = gameEventBusService;
        _combatService = combatService;
    }

    [ScriptFunction("cast", "Spell cast primitive placeholder. Returns false when no cast is executed.")]
    public bool Cast(uint npcSerial, int spellId, uint? targetSerial = null)
    {
        _ = npcSerial;
        _ = spellId;
        _ = targetSerial;

        // TODO: wire real spell system.
        return false;
    }

    [ScriptFunction("clear_target", "Clears combat target for the given npc.")]
    public bool ClearTarget(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        return _combatService.ClearCombatantAsync(npc!.Id).GetAwaiter().GetResult();
    }

    [ScriptFunction("set_target", "Sets combat target serial for the given npc.")]
    public bool SetTarget(uint npcSerial, uint targetSerial)
    {
        if (targetSerial == 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out _))
        {
            return false;
        }

        return _combatService.TrySetCombatantAsync(npc!.Id, (Serial)targetSerial)
                             .GetAwaiter()
                             .GetResult();
    }

    [ScriptFunction("swing", "Broadcasts a swing animation intent when target is in melee range.")]
    public bool Swing(uint npcSerial, uint targetSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var target))
        {
            return false;
        }

        if (npc!.MapId != target!.MapId || !npc.Location.InRange(target.Location, 1))
        {
            return false;
        }

        if (!AnimationUtils.TryResolveAnimation(
                AnimationIntent.SwingPrimary,
                npc.Body.Type,
                npc.IsMounted,
                out var animation
            ))
        {
            return false;
        }

        _gameEventBusService.PublishAsync(
                                new MobilePlayAnimationEvent(
                                    npc.Id,
                                    npc.MapId,
                                    npc.Location,
                                    animation.Action,
                                    animation.FrameCount,
                                    animation.RepeatCount,
                                    animation.Forward,
                                    animation.Repeat,
                                    animation.Delay
                                )
                            )
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();

        return true;
    }
}
