using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Tests.TestSupport;

/// <summary>Reads back what the converter writes under its loot destination: an array of tables
/// under <c>loot</c>, the same shape its own internal <c>LootTemplateFile</c> serializes.</summary>
internal sealed class ConvertedLootFile
{
    public List<LootTemplate> Loot { get; set; } = [];
}
