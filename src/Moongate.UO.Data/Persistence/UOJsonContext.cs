using System.Text.Json.Serialization;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Persistence;

[JsonSerializable(typeof(UOAccountEntity))]
[JsonSerializable(typeof(UOAccountCharacterEntity))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class UOJsonContext : JsonSerializerContext
{
}
