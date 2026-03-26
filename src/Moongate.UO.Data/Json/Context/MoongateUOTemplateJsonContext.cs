using System.Text.Json.Serialization;
using Moongate.UO.Data.Templates.Factions;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Loot;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Templates.SellProfiles;

namespace Moongate.UO.Data.Json.Context;

[JsonSourceGenerationOptions(
     PropertyNameCaseInsensitive = true,
     UseStringEnumConverter = true
 ), JsonSerializable(typeof(ItemTemplateDefinitionBase[])),
 JsonSerializable(typeof(ItemTemplateDefinition[])),
 JsonSerializable(typeof(HueSpec)),
 JsonSerializable(typeof(GoldValueSpec)),
 JsonSerializable(typeof(MobileTemplateDefinitionBase[])),
 JsonSerializable(typeof(MobileTemplateDefinition[])),
 JsonSerializable(typeof(MobileAiTemplate)),
 JsonSerializable(typeof(MobileVariantTemplate)),
 JsonSerializable(typeof(MobileAppearanceTemplate)),
 JsonSerializable(typeof(MobileEquipmentEntryTemplate)),
 JsonSerializable(typeof(MobileWeightedEquipmentItemTemplate)),
 JsonSerializable(typeof(ItemTemplateParamDefinition)),
 JsonSerializable(typeof(FactionDefinitionBase[])),
 JsonSerializable(typeof(FactionDefinition[])),
 JsonSerializable(typeof(LootTemplateDefinitionBase[])),
 JsonSerializable(typeof(LootTemplateDefinition[])),
 JsonSerializable(typeof(LootTemplateEntry[])),
 JsonSerializable(typeof(SellProfileTemplateDefinitionBase[])),
 JsonSerializable(typeof(SellProfileTemplateDefinition[])),
 JsonSerializable(typeof(SellProfileVendorItemDefinition[])),
 JsonSerializable(typeof(SellProfileAcceptedItemDefinition[]))]

/// <summary>
/// Represents MoongateUOTemplateJsonContext.
/// </summary>
public partial class MoongateUOTemplateJsonContext : JsonSerializerContext { }
