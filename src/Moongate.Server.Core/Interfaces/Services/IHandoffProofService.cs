using Moongate.Server.Core.Data.Realms;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Derives a short-lived credential key and verifies realm redirect proofs.
/// </summary>
public interface IHandoffProofService
{
    /// <summary>
    ///     Derives a credential key after the account password has been verified.
    /// </summary>
    byte[] DeriveCredentialKey(string username, string password);

    /// <summary>
    ///     Binds a credential key to the full account identity, target realm and redirect key.
    /// </summary>
    byte[] Sign(ReadOnlySpan<byte> credentialKey, PendingHandoff handoff, uint authKey);

    /// <summary>
    ///     Checks the full redirect identity against credentials presented to the game server.
    /// </summary>
    bool Verify(string username, string password, PendingHandoff handoff, uint authKey, ReadOnlySpan<byte> expected);
}
