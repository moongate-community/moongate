using System.Text.Json.Serialization;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json;
using Moongate.UO.Data.Json.Names;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;

namespace Moongate.UO.Data.Persistence;

// Persistence
[JsonSerializable(typeof(UOAccountEntity)), JsonSerializable(typeof(UOAccountCharacterEntity)),
 JsonSerializable(typeof(Serial)), JsonSerializable(typeof(UOMobileEntity)), JsonSerializable(typeof(UOItemEntity)),
 JsonSerializable(typeof(UOItemEntity[])), JsonSerializable(typeof(Point3D)), JsonSerializable(typeof(Point2D)),
 JsonSerializable(typeof(Point2D[])), JsonSerializable(typeof(Dictionary<Point2D, ItemReference>)),
 JsonSerializable(typeof(ItemReference)), JsonSerializable(typeof(ItemReference[])),
 JsonSerializable(typeof(Dictionary<ItemLayerType, ItemReference>)), JsonSerializable(typeof(ClientVersion)),
 JsonSerializable(typeof(ExpansionInfo)), JsonSerializable(typeof(ExpansionInfo[])), JsonSerializable(typeof(SkillInfo)),
 JsonSerializable(typeof(SkillEntry)), JsonSerializable(typeof(SkillEntry[])), JsonSerializable(typeof(List<SkillEntry>)),
 JsonSerializable(typeof(UOSkillName)), JsonSerializable(typeof(SkillInfo[])), JsonSerializable(typeof(JsonProfession)),
 JsonSerializable(typeof(JsonContainerSize[])), JsonSerializable(typeof(JsonContainerSize)),
 JsonSerializable(typeof(JsonRegion)), JsonSerializable(typeof(JsonRegionWrap)), JsonSerializable(typeof(JsonRegion[])),
 JsonSerializable(typeof(JsonMusic)), JsonSerializable(typeof(JsonMusic[])), JsonSerializable(typeof(JsonIntensityRange)),
 JsonSerializable(typeof(JsonIntensityRange[])), JsonSerializable(typeof(JsonWeather)),
 JsonSerializable(typeof(JsonWeather[])), JsonSerializable(typeof(JsonWeatherWrap)),
 JsonSerializable(typeof(ProfessionInfo)), JsonSerializable(typeof(JsonProfession[])),
 JsonSerializable(typeof(Dictionary<UOSkillName, double>)), JsonSerializable(typeof(JsonSkill)),
 JsonSerializable(typeof(JsonProfessionsRoot)), JsonSerializable(typeof(JsonStat)), JsonSerializable(typeof(JsonDfnHeader)),
 JsonSerializable(typeof(JsonNameDef)), JsonSerializable(typeof(JsonNameDef[])), JsonSourceGenerationOptions(
     WriteIndented = false,
     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
 )]
public partial class UOJsonContext : JsonSerializerContext { }
