using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Dispatches pending Lua brain event queues in deterministic order.
/// </summary>
internal static class LuaBrainPendingEventDispatcher
{
    public static void DispatchAll(Script luaScript, LuaBrainRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(luaScript);
        ArgumentNullException.ThrowIfNull(state);

        DispatchPendingSpeech(luaScript, state);
        DispatchPendingDeath(luaScript, state);
        DispatchPendingSpawn(luaScript, state);
        DispatchPendingInRange(luaScript, state);
        DispatchPendingOutRange(luaScript, state);
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
                    "death",
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

    private static void DispatchPendingSpeech(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingSpeech.Count > 0)
        {
            var speech = state.PendingSpeech.Dequeue();

            if (state.OnEventFunction is not null)
            {
                luaScript.Call(
                    state.OnEventFunction,
                    "speech_heard",
                    (uint)speech.SpeakerId,
                    BuildSpeechEventPayload(speech)
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

    private static void DispatchPendingSpawn(Script luaScript, LuaBrainRuntimeState state)
    {
        while (state.PendingSpawn.Count > 0)
        {
            var spawn = state.PendingSpawn.Dequeue();
            var payload = BuildSpawnEventPayload(spawn);

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
                "spawn",
                0u,
                payload
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
                "in_range",
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
                "out_range",
                (uint)outRange.SourceMobileId,
                outRange.Payload
            );
        }
    }

    private static Dictionary<string, object> BuildSpeechEventPayload(SpeechHeardEvent speech)
        => new()
        {
            ["listener_npc_id"] = (uint)speech.ListenerNpcId,
            ["speaker_id"] = (uint)speech.SpeakerId,
            ["text"] = speech.Text,
            ["speech_type"] = (byte)speech.SpeechType,
            ["map_id"] = speech.MapId,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = speech.Location.X,
                ["y"] = speech.Location.Y,
                ["z"] = speech.Location.Z
            }
        };

    private static Dictionary<string, object> BuildSpawnEventPayload(MobileSpawnedFromSpawnerEvent spawn)
        => new()
        {
            ["mobile_id"] = (uint)spawn.Mobile.Id,
            ["spawner_guid"] = spawn.SpawnerGuid.ToString("D"),
            ["spawner_name"] = spawn.SpawnerName,
            ["source_group"] = spawn.SourceGroup,
            ["source_file"] = spawn.SourceFile,
            ["spawn_count"] = spawn.SpawnCount,
            ["min_delay_ms"] = (int)spawn.MinDelay.TotalMilliseconds,
            ["max_delay_ms"] = (int)spawn.MaxDelay.TotalMilliseconds,
            ["team"] = spawn.Team,
            ["home_range"] = spawn.HomeRange,
            ["walking_range"] = spawn.WalkingRange,
            ["entry_name"] = spawn.EntryName,
            ["entry_max_count"] = spawn.EntryMaxCount,
            ["entry_probability"] = spawn.EntryProbability,
            ["map_id"] = spawn.Mobile.MapId,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = spawn.Mobile.Location.X,
                ["y"] = spawn.Mobile.Location.Y,
                ["z"] = spawn.Mobile.Location.Z
            },
            ["spawner_location"] = new Dictionary<string, int>
            {
                ["x"] = spawn.SpawnerLocation.X,
                ["y"] = spawn.SpawnerLocation.Y,
                ["z"] = spawn.SpawnerLocation.Z
            }
        };
}
