namespace Moongate.Server.Ultima.Data.Harvest;

/// <summary>
///     One <c>[[resource]]</c> of <c>data/harvest.toml</c>: something gathered from the world that runs out by area and
///     comes back with time, such as the fish of the sea.
/// </summary>
public class HarvestResource
{
    /// <summary>
    ///     The name scripts ask for it by: a lower-case identifier, such as <c>fish</c>.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     How many tiles on each side an area is: every cell of an area shares its amount. 1 to 256.
    /// </summary>
    public int Area { get; set; }

    /// <summary>
    ///     The least an area holds when it is full, at least 1.
    /// </summary>
    public int AmountMin { get; set; }

    /// <summary>
    ///     The most an area holds when it is full.
    /// </summary>
    public int AmountMax { get; set; }

    /// <summary>
    ///     The least minutes after the first take from a full area before it is full again. 0 by default.
    /// </summary>
    public int RespawnMinMinutes { get; set; }

    /// <summary>
    ///     The most minutes before it is full again; the same as the least when it is not set.
    /// </summary>
    public int RespawnMaxMinutes { get; set; }

    /// <summary>
    ///     The kinds an area may be of, one drawn by weight each time the area fills; none for a resource of one kind.
    /// </summary>
    public List<HarvestVein> Vein { get; set; } = [];
}
