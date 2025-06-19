using System.Text.Json.Serialization;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;

namespace Moongate.UO.Data.Persistence;

// Persistence
[JsonSerializable(typeof(UOAccountEntity))]
[JsonSerializable(typeof(UOAccountCharacterEntity))]
[JsonSerializable(typeof(Serial))]
[JsonSerializable(typeof(UOMobileEntity))]
[JsonConverter(
        typeof(JsonStringEnumConverter<Stat>)
    ),
]
[JsonSerializable(typeof(ClientVersion))]
[JsonSerializable(typeof(ExpansionInfo))]
[JsonSerializable(typeof(ExpansionInfo[]))]
[JsonSerializable(typeof(SkillInfo))]
[JsonSerializable(typeof(SkillInfo[]))]
[JsonSerializable(typeof(JsonProfession))]
[JsonSerializable(typeof(ProfessionInfo))]

[JsonSerializable(typeof(JsonSkill))]
[JsonSerializable(typeof(JsonProfessionsRoot))]
[JsonSerializable(typeof(JsonStat))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public partial class UOJsonContext : JsonSerializerContext
{
}
