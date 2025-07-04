using System.Text.Json.Serialization;
using Moongate.Core.Server.Json.Converters;

namespace Moongate.UO.Data.Factory;

public class ItemTemplate : BaseTemplate
{
    [JsonConverter(typeof(HexValueConverter<int>))]
    public int ItemId { get; set; }

    [JsonConverter(typeof(HexValueConverter<int>))]
    public int Hue { get; set; }

    [JsonConverter(typeof(RandomValueConverter<int>))]
    public int GoldValue { get; set; }
    public double Weight { get; set; } = 1.0;
    public bool Dyeable { get; set; }

    [JsonConverter(typeof(HexValueConverter<int>))]
    public int? GumpId { get; set; }

    public string ScriptId { get; set; }

    /// <summary>
    /// List of container names this item can be placed in.
    /// </summary>
    public List<string> Container { get; set; }
}
