namespace Moongate.Server.Ultima.Data.Crafts;

/// <summary>
///     The root of <c>data/crafts/resources.toml</c>: one <c>[[resource]]</c> per list.
/// </summary>
public class CraftResourcesFile
{
    public List<CraftResourceList> Resource { get; set; } = [];
}
