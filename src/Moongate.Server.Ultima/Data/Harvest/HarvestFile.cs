namespace Moongate.Server.Ultima.Data.Harvest;

/// <summary>
///     The root of <c>data/harvest.toml</c>: one <c>[[resource]]</c> per thing gathered.
/// </summary>
public class HarvestFile
{
    public List<HarvestResource> Resource { get; set; } = [];
}
