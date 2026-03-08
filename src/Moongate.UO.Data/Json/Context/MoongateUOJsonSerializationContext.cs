using System.Text.Json.Serialization;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Json.Locations;
using Moongate.UO.Data.Json.Names;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Json.Spawns;
using Moongate.UO.Data.Json.Teleporters;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Skills;

namespace Moongate.UO.Data.Json.Context;

[JsonSourceGenerationOptions(
     PropertyNameCaseInsensitive = true,
     UseStringEnumConverter = true
 ), JsonSerializable(typeof(SkillInfo[])),
 JsonSerializable(typeof(ExpansionInfo[])),
 JsonSerializable(typeof(JsonContainerSize[])),
 JsonSerializable(typeof(JsonNameDef[])),
 JsonSerializable(typeof(JsonMapLocations)),
 JsonSerializable(typeof(JsonLocationCategory)),
 JsonSerializable(typeof(JsonLocationDefinition)),
 JsonSerializable(typeof(JsonRegion[])),
 JsonSerializable(typeof(JsonRegion)),
 JsonSerializable(typeof(JsonBaseRegion)),
 JsonSerializable(typeof(JsonTownRegion)),
 JsonSerializable(typeof(JsonDungeonRegion)),
 JsonSerializable(typeof(JsonGuardedRegion)),
 JsonSerializable(typeof(JsonNoHousingRegion)),
 JsonSerializable(typeof(JsonGreenAcresRegion)),
 JsonSerializable(typeof(JsonJailRegion)),
 JsonSerializable(typeof(JsonRegionParent)),
 JsonSerializable(typeof(JsonWeatherWrap)),
 JsonSerializable(typeof(JsonProfessionsRoot)),
 JsonSerializable(typeof(JsonSpawnDefinition[])),
 JsonSerializable(typeof(JsonSpawnDefinition)),
 JsonSerializable(typeof(JsonSpawnEntryDefinition[])),
 JsonSerializable(typeof(JsonSpawnEntryDefinition)),
 JsonSerializable(typeof(JsonTeleporterDefinition[])),
 JsonSerializable(typeof(JsonTeleporterDefinition)),
 JsonSerializable(typeof(JsonTeleporterEndpoint))]

/// <summary>
/// Represents MoongateUOJsonSerializationContext.
/// </summary>
public partial class MoongateUOJsonSerializationContext : JsonSerializerContext { }
