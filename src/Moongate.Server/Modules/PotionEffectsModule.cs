using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Modules;

[ScriptModule("potion_effects", "Provides minimal potion use helpers for Lua item scripts.")]

/// <summary>
/// Exposes minimal consumable potion effects to Lua item scripts.
/// </summary>
public sealed class PotionEffectsModule
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IOutgoingPacketQueue? _outgoingPacketQueue;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IBackgroundJobService? _backgroundJobService;

    public PotionEffectsModule(
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue? outgoingPacketQueue = null,
        ISpatialWorldService? spatialWorldService = null,
        IBackgroundJobService? backgroundJobService = null
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _spatialWorldService = spatialWorldService;
        _backgroundJobService = backgroundJobService;
    }

    [ScriptFunction("restore_hits", "Restores hit points to the target mobile.")]
    public bool RestoreHits(uint mobileSerial, int amount)
    {
        if (amount <= 0 || !TryResolveMobile((Serial)mobileSerial, out var mobile) || !mobile!.IsAlive)
        {
            return false;
        }

        var maxHits = mobile.MaxHits > 0 ? mobile.MaxHits : Math.Max(1, mobile.Hits);
        mobile.Hits = Math.Clamp(mobile.Hits + amount, 0, maxHits);
        EnqueueStatusRefresh(mobile.Id, mobile);

        return true;
    }

    [ScriptFunction("restore_stamina", "Restores stamina to the target mobile.")]
    public bool RestoreStamina(uint mobileSerial, int amount)
    {
        if (amount <= 0 || !TryResolveMobile((Serial)mobileSerial, out var mobile) || !mobile!.IsAlive)
        {
            return false;
        }

        var maxStamina = mobile.MaxStamina > 0 ? mobile.MaxStamina : Math.Max(1, mobile.Stamina);
        mobile.Stamina = Math.Clamp(mobile.Stamina + amount, 0, maxStamina);
        EnqueueStatusRefresh(mobile.Id, mobile);

        return true;
    }

    [ScriptFunction("apply_temporary_strength", "Applies a temporary strength bonus and removes it later.")]
    public bool ApplyTemporaryStrength(uint mobileSerial, int bonus, int durationMs)
        => ApplyTemporaryModifier((Serial)mobileSerial, new() { StrengthBonus = bonus }, durationMs);

    [ScriptFunction("apply_temporary_dexterity", "Applies a temporary dexterity bonus and removes it later.")]
    public bool ApplyTemporaryDexterity(uint mobileSerial, int bonus, int durationMs)
        => ApplyTemporaryModifier((Serial)mobileSerial, new() { DexterityBonus = bonus }, durationMs);

    private bool ApplyTemporaryModifier(Serial mobileSerial, MobileModifierDelta delta, int durationMs)
    {
        if (mobileSerial == Serial.Zero ||
            durationMs < 0 ||
            _backgroundJobService is null ||
            !HasNonZeroStatDelta(delta) ||
            !TryResolveMobile(mobileSerial, out var mobile) ||
            !mobile!.IsAlive)
        {
            return false;
        }

        mobile.ApplyRuntimeModifier(delta);
        EnqueueStatusRefresh(mobile.Id, mobile);

        _backgroundJobService.EnqueueBackground(
            async () =>
            {
                if (durationMs > 0)
                {
                    await Task.Delay(durationMs).ConfigureAwait(false);
                }

                _backgroundJobService.PostToGameLoop(
                    () =>
                    {
                        mobile.RemoveRuntimeModifier(delta);
                        EnqueueStatusRefresh(mobile.Id, mobile);
                    }
                );
            }
        );

        return true;
    }

    private void EnqueueStatusRefresh(Serial mobileId, UOMobileEntity mobile)
    {
        if (_outgoingPacketQueue is null)
        {
            return;
        }

        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session))
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(mobile, 1));
        }
    }

    private static bool HasNonZeroStatDelta(MobileModifierDelta delta)
        => delta.StrengthBonus != 0 || delta.DexterityBonus != 0 || delta.IntelligenceBonus != 0;

    private bool TryResolveMobile(Serial mobileId, out UOMobileEntity? mobile)
    {
        mobile = null;

        if (mobileId == Serial.Zero)
        {
            return false;
        }

        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session) && session.Character is not null)
        {
            mobile = session.Character;

            return true;
        }

        if (_spatialWorldService is not null &&
            MobileScriptResolver.TryResolveMobile(_spatialWorldService, (uint)mobileId.Value, out mobile))
        {
            return true;
        }

        return false;
    }
}
