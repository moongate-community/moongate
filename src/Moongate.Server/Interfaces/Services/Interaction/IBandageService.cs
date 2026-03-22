using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates delayed self-bandage heals for mobiles.
/// </summary>
public interface IBandageService
{
    /// <summary>
    /// Attempts to start a self-bandage on the specified mobile.
    /// </summary>
    /// <param name="mobileId">Mobile serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when the self-bandage was started; otherwise <see langword="false" />.</returns>
    Task<bool> BeginSelfBandageAsync(Serial mobileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the specified mobile currently has an in-flight bandage timer.
    /// </summary>
    /// <param name="mobileId">Mobile serial identifier.</param>
    /// <returns><see langword="true" /> when the mobile is currently bandaging; otherwise <see langword="false" />.</returns>
    bool IsBandaging(Serial mobileId);

    /// <summary>
    /// Returns whether the specified mobile currently has at least one bandage in its backpack.
    /// </summary>
    /// <param name="mobileId">Mobile serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when a bandage stack exists; otherwise <see langword="false" />.</returns>
    Task<bool> HasBandageAsync(Serial mobileId, CancellationToken cancellationToken = default);
}
