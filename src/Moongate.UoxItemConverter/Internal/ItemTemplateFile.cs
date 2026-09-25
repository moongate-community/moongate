using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     The root of one converted item TOML file: an array of tables under
///     <c>
///         item
///     </c>
///     .
/// </summary>
internal sealed class ItemTemplateFile
{
    public List<ItemTemplate> Item { get; set; } = [];
}
