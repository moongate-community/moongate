namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     A <c>[[resource]]</c> of <c>data/crafts/resources.toml</c>: the item templates that count for a resource of the
///     recipes, such as the plain boards for <c>wood</c>.
/// </summary>
public class CraftResourceList
{
    /// <summary>
    ///     The name recipes give it, a lower-case identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The item templates that count for it, at least one.
    /// </summary>
    public List<string> Templates { get; set; } = [];
}
