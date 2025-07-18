using System.Text.Json.Serialization;
using Moongate.Core.Server.Json.Converters;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Factory;

public class ItemTemplate : BaseTemplate
{
    [JsonConverter(typeof(HexValueConverter<int>))]
    public int ItemId { get; set; }

    [JsonConverter(typeof(HexValueConverter<int>))]
    public int Hue { get; set; }

    [JsonConverter(typeof(RandomValueConverter<int>))]
    public int GoldValue { get; set; }

    public int Weight { get; set; } = 1;
    public bool Dyeable { get; set; } = true;

    public LootType LootType { get; set; } = LootType.Regular;

    public bool Stackable { get; set; } = true;

    [JsonConverter(typeof(HexValueConverter<int>))]
    public int? GumpId { get; set; }

    public string ScriptId { get; set; }

    public bool IsMovable { get; set; } = true;

    /// <summary>
    /// List of container names this item can be placed in.
    /// </summary>
    public List<string> Container { get; set; } = new();
}
