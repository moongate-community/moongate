using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Items;

/// <summary>
/// Provides server-side door detection and toggle operations.
/// </summary>
public interface IDoorService
{
    /// <summary>
    /// Determines whether the specified item serial is a supported door.
    /// </summary>
    /// <param name="itemId">Item serial.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the item resolves to a supported door; otherwise <c>false</c>.</returns>
    Task<bool> IsDoorAsync(Serial itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a supported door between opened and closed states.
    /// </summary>
    /// <param name="itemId">Item serial.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when toggled; otherwise <c>false</c>.</returns>
    Task<bool> ToggleAsync(Serial itemId, CancellationToken cancellationToken = default);
}
