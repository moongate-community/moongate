using System.Text.Json.Serialization;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Json.Regions;

/// <summary>
/// Represents a region entry from ModernUO-style regions.json.
/// </summary>
[JsonConverter(typeof(JsonRegionConverter))]
public class JsonRegion
{
    [JsonIgnore]
    public int Id { get; set; }

    [JsonPropertyName("$type")]
    public string Type { get; set; }

    public string Map { get; set; }

    [JsonIgnore]
    public int MapId => RegionMapIdResolver.Resolve(Map);

    public string Name { get; set; }

    public int Priority { get; set; }

    public MusicName? Music { get; set; }

    public List<JsonCoordinate> Area { get; set; } = [];
}

/// <summary>
/// Represents ModernUO BaseRegion.
/// </summary>
public sealed class JsonBaseRegion : JsonRegion { }

/// <summary>
/// Represents ModernUO TownRegion.
/// </summary>
public sealed class JsonTownRegion : JsonRegion
{
    public bool NoLogoutDelay { get; set; }

    public JsonRegionParent? Parent { get; set; }

    public bool GuardsDisabled { get; set; }

    public Point3D? Entrance { get; set; }

    public Point3D? GoLocation { get; set; }
}

/// <summary>
/// Represents ModernUO DungeonRegion.
/// </summary>
public sealed class JsonDungeonRegion : JsonRegion
{
    public Point3D? Entrance { get; set; }

    public Point3D? GoLocation { get; set; }
}

/// <summary>
/// Represents ModernUO GuardedRegion.
/// </summary>
public sealed class JsonGuardedRegion : JsonRegion
{
    public Point3D? GoLocation { get; set; }
}

/// <summary>
/// Represents ModernUO NoHousingRegion.
/// </summary>
public sealed class JsonNoHousingRegion : JsonRegion { }

/// <summary>
/// Represents ModernUO GreenAcresRegion.
/// </summary>
public sealed class JsonGreenAcresRegion : JsonRegion { }

/// <summary>
/// Represents ModernUO JailRegion.
/// </summary>
public sealed class JsonJailRegion : JsonRegion { }

/// <summary>
/// Represents a parent region reference in ModernUO regions.json.
/// </summary>
public sealed class JsonRegionParent
{
    public string Name { get; set; }

    public string Map { get; set; }

    [JsonIgnore]
    public int MapId => RegionMapIdResolver.Resolve(Map);
}

internal static class RegionMapIdResolver
{
    public static int Resolve(string? map)
        => map?.ToLowerInvariant() switch
        {
            "felucca" => 0,
            "trammel" => 1,
            "ilshenar" => 2,
            "malas" => 3,
            "tokuno" => 4,
            "termur" => 5,
            "internal" => 0x7F,
            _ => 0
        };
}
