using Moongate.Server.Abstractions.Data;

namespace Moongate.Server.Abstractions.Interfaces.Accounts;

/// <summary>
/// Correlates a login to the client's game-port reconnect via a single-use, expiring auth key.
/// </summary>
public interface IPendingLoginStore
{
    /// <summary>Stores <paramref name="login" /> and returns a fresh non-zero auth key.</summary>
    uint Create(PendingLogin login);

    /// <summary>Consumes the entry for <paramref name="authKey" />; false if unknown or expired.</summary>
    bool TryTake(uint authKey, out PendingLogin login);
}
