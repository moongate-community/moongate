using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Events.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Builds Lua brain event payload dictionaries.
/// </summary>
internal static class LuaBrainPayloadFactory
{
    public static Dictionary<string, object> BuildSpeechEventPayload(SpeechHeardEvent speech)
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

    public static Dictionary<string, object> BuildSpawnEventPayload(MobileSpawnedFromSpawnerEvent spawn)
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

    public static Dictionary<string, object> BuildInRangeEventPayload(
        Serial listenerNpcId,
        UOMobileEntity sourceMobile,
        int range
    )
        => new()
        {
            ["listener_npc_id"] = (uint)listenerNpcId,
            ["source_mobile_id"] = (uint)sourceMobile.Id,
            ["source_is_player"] = sourceMobile.IsPlayer,
            ["map_id"] = sourceMobile.MapId,
            ["range"] = range,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = sourceMobile.Location.X,
                ["y"] = sourceMobile.Location.Y,
                ["z"] = sourceMobile.Location.Z
            }
        };
}
