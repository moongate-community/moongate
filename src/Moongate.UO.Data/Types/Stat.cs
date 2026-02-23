using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Converters;

namespace Moongate.UO.Data.Types;

[JsonConverter(typeof(StatJsonConverter))]
/// <summary>
/// Represents Stat.
/// </summary>
public enum Stat
{
    Strength,
    Dexterity,
    Intelligence
}
