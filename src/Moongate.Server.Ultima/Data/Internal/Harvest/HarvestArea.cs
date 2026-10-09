namespace Moongate.Server.Ultima.Data.Internal.Harvest;

/// <summary>
///     What is left in one area of a resource, and when it is full again.
/// </summary>
internal sealed class HarvestArea
{
    /// <summary>
    ///     How much is left.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     The timestamp at which the area refills, set at the first take from a full area; null while it is full.
    /// </summary>
    public long? RefillAt { get; set; }
}
