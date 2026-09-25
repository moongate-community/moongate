namespace Moongate.Server.Core.Interfaces.Admin;

/// <summary>
///     Bounds administrative login attempts across hosts.
/// </summary>
public interface IAdminLoginThrottle
{
    /// <summary>
    ///     Consumes one attempt for the direct peer and case-sensitive account username.
    /// </summary>
    Task<bool> TryAcquireAsync(string peerAddress, string username, CancellationToken token = default);
}
