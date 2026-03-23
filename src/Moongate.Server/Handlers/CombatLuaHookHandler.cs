using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class CombatLuaHookHandler
    : IGameEventListener<CombatHitEvent>,
      IGameEventListener<CombatMissEvent>,
      IMoongateService
{
    private readonly ILuaBrainRunner _luaBrainRunner;

    public CombatLuaHookHandler(ILuaBrainRunner luaBrainRunner)
    {
        _luaBrainRunner = luaBrainRunner;
    }

    public Task HandleAsync(CombatHitEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnqueueIfNpc(
            gameEvent.Attacker,
            LuaBrainCombatHookType.Attack,
            gameEvent.Defender.Id,
            CreatePayload(gameEvent.Attacker, gameEvent.Defender, gameEvent.Damage)
        );
        EnqueueIfNpc(
            gameEvent.Defender,
            LuaBrainCombatHookType.Attacked,
            gameEvent.Attacker.Id,
            CreatePayload(gameEvent.Attacker, gameEvent.Defender, gameEvent.Damage)
        );

        return Task.CompletedTask;
    }

    public Task HandleAsync(CombatMissEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnqueueIfNpc(
            gameEvent.Attacker,
            LuaBrainCombatHookType.MissedAttack,
            gameEvent.Defender.Id,
            CreatePayload(gameEvent.Attacker, gameEvent.Defender)
        );
        EnqueueIfNpc(
            gameEvent.Defender,
            LuaBrainCombatHookType.MissedByAttack,
            gameEvent.Attacker.Id,
            CreatePayload(gameEvent.Attacker, gameEvent.Defender)
        );

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    private static Dictionary<string, object?> CreatePayload(
        UOMobileEntity attacker,
        UOMobileEntity defender,
        int? damage = null
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["map_id"] = attacker.MapId,
            ["x"] = attacker.Location.X,
            ["y"] = attacker.Location.Y,
            ["z"] = attacker.Location.Z,
            ["is_player_attacker"] = attacker.IsPlayer,
            ["is_player_defender"] = defender.IsPlayer
        };

        if (damage.HasValue)
        {
            payload["damage"] = damage.Value;
        }

        return payload;
    }

    private void EnqueueIfNpc(
        UOMobileEntity mobile,
        LuaBrainCombatHookType hookType,
        Serial otherMobileId,
        Dictionary<string, object?> payload
    )
    {
        if (mobile.IsPlayer)
        {
            return;
        }

        _luaBrainRunner.EnqueueCombatHook(mobile.Id, new(hookType, otherMobileId, payload));
    }
}
