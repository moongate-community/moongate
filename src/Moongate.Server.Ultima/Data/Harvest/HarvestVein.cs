namespace Moongate.Server.Ultima.Data.Harvest;

/// <summary>
///     One <c>[[resource.vein]]</c> of <c>data/harvest.toml</c>: a kind of the resource an area may be of, such as the
///     oak among the wood.
/// </summary>
public class HarvestVein
{
    /// <summary>
    ///     The name scripts get it by: a lower-case identifier, such as <c>oak</c>.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     How often an area is of this kind, against the weights of the other veins of the resource. 1 to 1000000.
    /// </summary>
    public int Weight { get; set; }
}
