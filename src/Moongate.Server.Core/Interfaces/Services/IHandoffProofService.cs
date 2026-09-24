namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Derives a short-lived credential key and verifies realm redirect proofs.</summary>
public interface IHandoffProofService
{
    /// <summary>Derives a credential key after the account password has been verified.</summary>
    byte[] DeriveCredentialKey(string username, string password);

    /// <summary>Binds a credential key to one realm and redirect key.</summary>
    byte[] Sign(ReadOnlySpan<byte> credentialKey, string realmId, uint authKey);

    /// <summary>Checks a redirect proof against the credentials presented to the game server.</summary>
    bool Verify(string username, string password, string realmId, uint authKey, ReadOnlySpan<byte> expected);
}
