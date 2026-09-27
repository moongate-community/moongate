namespace Moongate.Server.Ultima.Data.Templates.Items;

/// <summary>
///     One file under <c>templates/loots/</c>: a <c>[[loot]]</c> array of <see cref="LootTemplate" />.
/// </summary>
public class LootTemplateFile
{
    /// <summary>
    ///     The tables in the file.
    /// </summary>
    public List<LootTemplate> Loot { get; set; } = [];
}
