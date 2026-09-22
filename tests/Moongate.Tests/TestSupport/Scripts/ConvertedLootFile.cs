using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.Tests.TestSupport.Scripts;

/// <summary>Reads back what <c>UoxItemConverter.cs</c> writes under its loot destination: an array of
/// tables under <c>loot</c>, the same shape the script's own private <c>LootTemplateFile</c> serializes.</summary>
internal sealed class ConvertedLootFile
{
    public List<LootTemplate> Loot { get; set; } = [];
}
