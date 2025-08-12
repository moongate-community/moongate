using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moongate.UO.Data.Factory.Json;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(BaseTemplate))]
[JsonSerializable(typeof(BaseTemplate[]))]
[JsonSerializable(typeof(ItemTemplate))]
[JsonSerializable(typeof(MobileTemplate))]
[JsonSerializable(typeof(RandomEquipmentPoolTemplate))]
[JsonSerializable(typeof(RandomEquipmentPoolTemplate[]))]
[JsonSerializable(typeof(WeightedEquipmentItem))]
[JsonSerializable(typeof(WeightedEquipmentItem[]))]
[JsonSerializable(typeof(EquipmentItemTemplate))]
[JsonSerializable(typeof(EquipmentItemTemplate[]))]
public partial class TextJsonTemplateContext : JsonSerializerContext
{
}
