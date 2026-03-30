using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;
using Moongate.UO.Data.Types;

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
    private readonly IMagicService? _magicService;

    public CombatModule(
        ISpatialWorldService spatialWorldService,
        IGameEventBusService gameEventBusService,
        ICombatService combatService,
        IMagicService? magicService = null
    )
    {
        _spatialWorldService = spatialWorldService;
        _gameEventBusService = gameEventBusService;
        _combatService = combatService;
        _magicService = magicService;
    }

    [ScriptFunction("cast", "Starts a spell cast for the resolved npc and optionally binds a target.")]
    public bool Cast(uint npcSerial, int spellId, uint? targetSerial = null)
    {
        if (_magicService is null ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        var castSucceeded = _magicService.TryCastAsync(npc!, spellId).AsTask().GetAwaiter().GetResult();

        if (!castSucceeded)
        {
            return false;
        }

        if (targetSerial is null)
        {
            return true;
        }

        if (targetSerial.Value == 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial.Value, out var target) ||
            !_magicService.TrySetTarget(npc!.Id, spellId, target!.Id))
        {
            _magicService.Interrupt(npc!.Id);

            return false;
        }

        return true;
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

    [ScriptFunction("attack_range", "Returns current attack range for the given npc based on the equipped weapon.")]
    public int GetAttackRange(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return 1;
        }

        var range = ResolveWeapon(npc!)?.CombatStats?.RangeMax ?? 0;

        return range > 0 ? range : 1;
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

    private static UOItemEntity? ResolveWeapon(UOMobileEntity mobile)
        => mobile.GetEquippedItemsRuntime()
                 .FirstOrDefault(
                     item => item.EquippedLayer is ItemLayerType.OneHanded or
                                                     ItemLayerType.TwoHanded or
                                                     ItemLayerType.FirstValid
                 );
}
