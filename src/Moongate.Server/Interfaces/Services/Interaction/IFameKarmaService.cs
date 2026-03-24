using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates fame and karma awards after a player kills a non-player mobile.
/// </summary>
public interface IFameKarmaService
{
    /// <summary>
    /// Applies fame and karma awards to the killer when the award gate is satisfied.
    /// </summary>
    /// <param name="victim">The defeated mobile.</param>
    /// <param name="killer">The mobile that dealt the killing blow.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the award has been processed.</returns>
    Task AwardNpcKillAsync(
        UOMobileEntity victim,
        UOMobileEntity killer,
        CancellationToken cancellationToken = default
    );
}
