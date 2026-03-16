using System.Text.Json.Serialization;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Scripting;

namespace Moongate.Server.Json;

[JsonSourceGenerationOptions(
     PropertyNameCaseInsensitive = true,
     UseStringEnumConverter = true,
     WriteIndented = true
 ), JsonSerializable(typeof(MoongateConfig)), JsonSerializable(typeof(MoongateHttpConfig)),
 JsonSerializable(typeof(MoongateGameConfig)), JsonSerializable(typeof(MoongateMetricsConfig)),
 JsonSerializable(typeof(MoongatePersistenceConfig)), JsonSerializable(typeof(MoongateSpatialConfig)),
 JsonSerializable(typeof(MoongateEmailConfig)), JsonSerializable(typeof(MoongateEmailSmtpConfig)),
 JsonSerializable(typeof(NpcDialogueResponse))]

/// <summary>
/// Represents MoongateServerJsonContext.
/// </summary>
public partial class MoongateServerJsonContext : JsonSerializerContext { }
