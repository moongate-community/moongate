namespace Moongate.Server.Ultima.Data.Templates.Items;

/// <summary>
///     One file under <c>templates/items/</c>: a <c>[[item]]</c> array of <see cref="ItemTemplate" />.
/// </summary>
public class ItemTemplateFile
{
    /// <summary>
    ///     The templates in the file.
    /// </summary>
    public List<ItemTemplate> Item { get; set; } = [];
}
