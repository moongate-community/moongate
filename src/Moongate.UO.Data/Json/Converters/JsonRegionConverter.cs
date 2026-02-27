using System.Text.Json;
using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Regions;

namespace Moongate.UO.Data.Json.Converters;

/// <summary>
/// Handles polymorphic deserialization for ModernUO region entries based on "$type".
/// </summary>
public sealed class JsonRegionConverter : JsonConverter<JsonRegion>
{
    public override JsonRegion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var runtimeType = ResolveRegionType(root);
        var json = root.GetRawText();
        var region = (JsonRegion?)JsonSerializer.Deserialize(json, runtimeType, options);

        return region ?? new JsonRegion();
    }

    public override void Write(Utf8JsonWriter writer, JsonRegion value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);

    private static Type ResolveRegionType(JsonElement root)
    {
        if (!root.TryGetProperty("$type", out var typeProperty) ||
            typeProperty.ValueKind is not JsonValueKind.String)
        {
            return typeof(JsonRegion);
        }

        var typeName = typeProperty.GetString();

        return typeName switch
        {
            "BaseRegion" => typeof(JsonBaseRegion),
            "TownRegion" => typeof(JsonTownRegion),
            "DungeonRegion" => typeof(JsonDungeonRegion),
            "GuardedRegion" => typeof(JsonGuardedRegion),
            "NoHousingRegion" => typeof(JsonNoHousingRegion),
            "GreenAcresRegion" => typeof(JsonGreenAcresRegion),
            "JailRegion" => typeof(JsonJailRegion),
            _ => typeof(JsonRegion)
        };
    }
}
