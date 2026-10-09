namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Makes the pets less loyal as time goes by, and wild when none is left.
/// </summary>
public interface IPetLoyaltyService
{
    /// <summary>
    ///     Takes the loyalty drain off every owned creature of the world: one that is low on loyalty shows it, one with none
    ///     left goes wild. The timer calls it every <c>ultima.pets.loyalty_drain_minutes</c>.
    /// </summary>
    void Drain();
}
