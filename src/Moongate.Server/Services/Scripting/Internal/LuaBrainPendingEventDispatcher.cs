using Moongate.Server.Data.Internal.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Dispatches pending Lua brain event queues in deterministic order.
/// </summary>
internal static class LuaBrainPendingEventDispatcher
{
    private static readonly DynValue SpeechHeardEventName = DynValue.NewString("speech_heard");
    private static readonly DynValue DeathEventName = DynValue.NewString("death");
    private static readonly DynValue SpawnEventName = DynValue.NewString("spawn");
    private static readonly DynValue AttackEventName = DynValue.NewString("attack");
    private static readonly DynValue MissedAttackEventName = DynValue.NewString("missed_attack");
    private static readonly DynValue AttackedEventName = DynValue.NewString("attacked");
    private static readonly DynValue MissedByAttackEventName = DynValue.NewString("missed_by_attack");
    private static readonly DynValue InRangeEventName = DynValue.NewString("in_range");
    private static readonly DynValue OutRangeEventName = DynValue.NewString("out_range");

    public static void DispatchAll(Script luaScript, LuaBrainRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(luaScript);
        ArgumentNullException.ThrowIfNull(state);

        DispatchPendingSpeech(luaScript, state);
        DispatchPendingDeath(luaScript, state);
        DispatchPendingSpawn(luaScript, state);
        DispatchPendingCombat(luaScript, state);
        DispatchPendingInRange(luaScript, state);
        DispatchPendingOutRange(luaScript, state);
    }

    private static void DispatchPendingCombat(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingCombatHooks.Count > 0)
        {
            var combat = state.PendingCombatHooks.Dequeue();
            var actorId = DynValue.NewNumber((uint)combat.OtherMobileId);
            var payload = DynValue.FromObject(luaScript, combat.Payload);
            var hookFunction = ResolveCombatHook(state, combat.HookType);

            if (hookFunction is not null)
            {
                luaScript.Call(hookFunction, actorId, payload);
                continue;
            }

            if (state.OnEventFunction is null)
            {
                continue;
            }

            luaScript.Call(
                state.OnEventFunction,
                ResolveCombatEventName(combat.HookType),
                actorId,
                payload
            );
        }
    }

    private static void DispatchPendingDeath(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingDeath.Count > 0)
        {
            var death = state.PendingDeath.Dequeue();
            var byCharacter = death.ByCharacterId.HasValue
                                  ? DynValue.NewNumber((uint)death.ByCharacterId.Value)
                                  : DynValue.Nil;

            if (state.OnEventFunction is not null)
            {
                luaScript.Call(
                    state.OnEventFunction,
                    DeathEventName,
                    byCharacter,
                    death.Context
                );

                continue;
            }

            if (state.OnDeathFunction is null)
            {
                continue;
            }

            luaScript.Call(
                state.OnDeathFunction,
                byCharacter,
                death.Context
            );
        }
    }

    private static void DispatchPendingInRange(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingInRange.Count > 0)
        {
            var inRange = state.PendingInRange.Dequeue();

            if (state.OnInRangeFunction is not null)
            {
                luaScript.Call(
                    state.OnInRangeFunction,
                    (uint)state.MobileId,
                    (uint)inRange.SourceMobileId,
                    inRange.Payload
                );

                continue;
            }

            if (state.OnEventFunction is null)
            {
                continue;
            }

            luaScript.Call(
                state.OnEventFunction,
                InRangeEventName,
                (uint)inRange.SourceMobileId,
                inRange.Payload
            );
        }
    }

    private static void DispatchPendingOutRange(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingOutRange.Count > 0)
        {
            var outRange = state.PendingOutRange.Dequeue();

            if (state.OnOutRangeFunction is not null)
            {
                luaScript.Call(
                    state.OnOutRangeFunction,
                    (uint)state.MobileId,
                    (uint)outRange.SourceMobileId,
                    outRange.Payload
                );

                continue;
            }

            if (state.OnEventFunction is null)
            {
                continue;
            }

            luaScript.Call(
                state.OnEventFunction,
                OutRangeEventName,
                (uint)outRange.SourceMobileId,
                outRange.Payload
            );
        }
    }

    private static void DispatchPendingSpawn(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingSpawn.Count > 0)
        {
            var spawn = state.PendingSpawn.Dequeue();
            var payload = LuaBrainPayloadFactory.BuildSpawnEventPayload(spawn);

            if (state.OnSpawnFunction is not null)
            {
                luaScript.Call(
                    state.OnSpawnFunction,
                    (uint)state.MobileId,
                    payload
                );

                continue;
            }

            if (state.OnEventFunction is null)
            {
                continue;
            }

            luaScript.Call(
                state.OnEventFunction,
                SpawnEventName,
                0u,
                payload
            );
        }
    }

    private static void DispatchPendingSpeech(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingSpeech.Count > 0)
        {
            var speech = state.PendingSpeech.Dequeue();

            if (state.OnEventFunction is not null)
            {
                luaScript.Call(
                    state.OnEventFunction,
                    SpeechHeardEventName,
                    (uint)speech.SpeakerId,
                    LuaBrainPayloadFactory.BuildSpeechEventPayload(speech)
                );

                continue;
            }

            if (state.OnSpeechFunction is null)
            {
                continue;
            }

            luaScript.Call(
                state.OnSpeechFunction,
                (uint)speech.ListenerNpcId,
                (uint)speech.SpeakerId,
                speech.Text,
                (byte)speech.SpeechType,
                speech.MapId,
                speech.Location.X,
                speech.Location.Y,
                speech.Location.Z
            );
        }
    }

    private static DynValue? ResolveCombatHook(LuaBrainRuntimeState state, LuaBrainCombatHookType hookType)
        => hookType switch
        {
            LuaBrainCombatHookType.Attack => state.OnAttackFunction,
            LuaBrainCombatHookType.MissedAttack => state.OnMissedAttackFunction,
            LuaBrainCombatHookType.Attacked => state.OnAttackedFunction,
            LuaBrainCombatHookType.MissedByAttack => state.OnMissedByAttackFunction,
            _ => null
        };

    private static DynValue ResolveCombatEventName(LuaBrainCombatHookType hookType)
        => hookType switch
        {
            LuaBrainCombatHookType.Attack => AttackEventName,
            LuaBrainCombatHookType.MissedAttack => MissedAttackEventName,
            LuaBrainCombatHookType.Attacked => AttackedEventName,
            LuaBrainCombatHookType.MissedByAttack => MissedByAttackEventName,
            _ => AttackEventName
        };
}
