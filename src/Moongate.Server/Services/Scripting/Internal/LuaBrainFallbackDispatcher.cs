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

            scriptEngineService.CallFunction("on_event", "death", byCharacterId, death.Context);
            scriptEngineService.CallFunction("on_death", byCharacterId, death.Context);
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
