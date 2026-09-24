using Moongate.Server.Core.Data.Realms;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Transfers one authenticated login identity to its selected game realm.</summary>
public interface IGameHandoffStore
{
    /// <summary>Issues a short-lived redirect key bound to the account credentials and target realm.</summary>
    ValueTask<uint> IssueAsync(PendingHandoff handoff, ReadOnlyMemory<byte> credentialKey,
        CancellationToken token = default);

    /// <summary>Consumes a valid redirect exactly once after verifying the game login credentials.</summary>
    ValueTask<PendingHandoff?> RedeemAsync(string realmId, Guid instanceId, uint authKey,
        string username, string password, CancellationToken token = default);

    /// <summary>Removes a redirect that could not be delivered to the login client.</summary>
    ValueTask RevokeAsync(string realmId, uint authKey, CancellationToken token = default);
}
