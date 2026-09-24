using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Tests.TestSupport;

/// <summary>
/// Reads back what the converter writes under its item destination: an array of tables
/// under <c>item</c>, the same shape its own internal <c>ItemTemplateFile</c> serializes.
/// </summary>
internal sealed class ConvertedItemFile
{
    public List<ItemTemplate> Item { get; set; } = [];
}
