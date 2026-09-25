using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Admin;

namespace Moongate.Server.Core.Interfaces.Admin;

/// <summary>
///     Shared, fail-closed administrative sessions independent of game handoff tickets.
/// </summary>
public interface IAdminSessionStore
{
    /// <summary>
    ///     Reads the account authorization generation; absence invalidates sessions.
    /// </summary>
    Task<AdminAccountGate?> ReadGateAsync(Serial accountId, CancellationToken token = default);

    /// <summary>
    ///     Rotates the generation and revokes all sessions. Requires the account SQL row lock.
    /// </summary>
    Task<AdminAccountGate> ResetGateAsync(Serial accountId, bool blocked, CancellationToken token = default);

    /// <summary>
    ///     Opens only the expected generation. Requires the account SQL row lock.
    /// </summary>
    Task<bool> TryOpenGateAsync(Serial accountId, Guid generation, CancellationToken token = default);

    /// <summary>
    ///     Issues a bounded-lifetime session only for the current open generation.
    /// </summary>
    Task<AdminSession> IssueAsync(
        AdminIdentity identity,
        Guid generation,
        string tokenHash,
        TimeSpan lifetime,
        CancellationToken token = default
    );

    /// <summary>
    ///     Returns an unexpired session only if its account gate still authorizes it.
    /// </summary>
    Task<AdminSession?> FindAsync(string tokenHash, CancellationToken token = default);

    /// <summary>
    ///     Idempotently removes one session and its account index entry.
    /// </summary>
    Task RemoveAsync(string tokenHash, CancellationToken token = default);
}
