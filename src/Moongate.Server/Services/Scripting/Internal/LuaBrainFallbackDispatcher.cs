using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Internal.Scripting;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Dispatches pending brain events via generic script engine fallback hooks.
/// </summary>
internal static class LuaBrainFallbackDispatcher
{
    public static void DispatchAll(IScriptEngineService scriptEngineService, LuaBrainRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(scriptEngineService);
        ArgumentNullException.ThrowIfNull(state);

        while (state.PendingSpawn.Count > 0)
        {
            var spawn = state.PendingSpawn.Dequeue();
            var payload = LuaBrainPayloadFactory.BuildSpawnEventPayload(spawn);

            scriptEngineService.CallFunction("on_spawn", (uint)state.MobileId, payload);
            scriptEngineService.CallFunction("on_event", "spawn", 0u, payload);
        }

        while (state.PendingCombatHooks.Count > 0)
        {
            var combat = state.PendingCombatHooks.Dequeue();
            var eventName = combat.HookType switch
            {
                LuaBrainCombatHookType.Attack         => "attack",
                LuaBrainCombatHookType.MissedAttack   => "missed_attack",
                LuaBrainCombatHookType.Attacked       => "attacked",
                LuaBrainCombatHookType.MissedByAttack => "missed_by_attack",
                _                                     => "attack"
            };
            var hookName = combat.HookType switch
            {
                LuaBrainCombatHookType.Attack         => "on_attack",
                LuaBrainCombatHookType.MissedAttack   => "on_missed_attack",
                LuaBrainCombatHookType.Attacked       => "on_attacked",
                LuaBrainCombatHookType.MissedByAttack => "on_missed_by_attack",
                _                                     => "on_attack"
            };

            scriptEngineService.CallFunction("on_event", eventName, (uint)combat.OtherMobileId, combat.Payload);
            scriptEngineService.CallFunction(hookName, (uint)combat.OtherMobileId, combat.Payload);
        }

        while (state.PendingSpeech.Count > 0)
        {
            var speech = state.PendingSpeech.Dequeue();
            scriptEngineService.CallFunction(
                "on_event",
                "speech_heard",
                (uint)speech.SpeakerId,
                LuaBrainPayloadFactory.BuildSpeechEventPayload(speech)
            );
            scriptEngineService.CallFunction(
                "on_speech",
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

        while (state.PendingDeath.Count > 0)
        {
            var death = state.PendingDeath.Dequeue();
            var byCharacterId = death.ByCharacterId.HasValue ? (uint)death.ByCharacterId.Value : 0u;
            var eventName = death.HookType switch
            {
                LuaBrainDeathHookType.BeforeDeath => "before_death",
                LuaBrainDeathHookType.AfterDeath  => "after_death",
                _                                 => "death"
            };
            var hookName = death.HookType switch
            {
                LuaBrainDeathHookType.BeforeDeath => "on_before_death",
                LuaBrainDeathHookType.AfterDeath  => "on_after_death",
                _                                 => "on_death"
            };

            scriptEngineService.CallFunction("on_event", eventName, byCharacterId, death.Context);
            scriptEngineService.CallFunction(hookName, byCharacterId, death.Context);
        }

        while (state.PendingInRange.Count > 0)
        {
            var inRange = state.PendingInRange.Dequeue();
            scriptEngineService.CallFunction("on_event", "in_range", (uint)inRange.SourceMobileId, inRange.Payload);
            scriptEngineService.CallFunction(
                "on_in_range",
                (uint)state.MobileId,
                (uint)inRange.SourceMobileId,
                inRange.Payload
            );
        }

        while (state.PendingOutRange.Count > 0)
        {
            var outRange = state.PendingOutRange.Dequeue();
            scriptEngineService.CallFunction("on_event", "out_range", (uint)outRange.SourceMobileId, outRange.Payload);
            scriptEngineService.CallFunction(
                "on_out_range",
                (uint)state.MobileId,
                (uint)outRange.SourceMobileId,
                outRange.Payload
            );
        }

        scriptEngineService.CallFunction("on_brain_tick", (uint)state.MobileId);
    }
}
