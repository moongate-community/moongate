using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Loot;

public sealed class LootTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public LootTemplateModeType Mode { get; set; } = LootTemplateModeType.Weighted;
    public int Rolls { get; set; } = 1;
    public int NoDropWeight { get; set; }
    public List<LootTemplateEntry> Entries { get; set; } = [];
}
