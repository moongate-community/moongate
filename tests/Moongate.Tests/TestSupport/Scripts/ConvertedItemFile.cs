using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.Tests.TestSupport.Scripts;

/// <summary>Reads back what <c>UoxItemConverter.cs</c> writes under its item destination: an array of
/// tables under <c>item</c>, the same shape the script's own private <c>ItemTemplateFile</c> serializes.</summary>
internal sealed class ConvertedItemFile
{
    public List<ItemTemplate> Item { get; set; } = [];
}
