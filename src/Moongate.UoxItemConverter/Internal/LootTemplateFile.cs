using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>The root of one converted loot TOML file: an array of tables under <c>loot</c>.</summary>
internal sealed class LootTemplateFile
{
    public List<LootTemplate> Loot { get; set; } = [];
}
