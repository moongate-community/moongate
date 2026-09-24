using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Realms;

/// <summary>Creates domain-separated HMAC proofs for the login-to-game handoff.</summary>
public sealed class HandoffProofService : IHandoffProofService, IDisposable
{
    private const int CredentialKeySize = 32;
    private const int LengthPrefixSize = sizeof(int);
    private const int RedirectKeySize = sizeof(uint);

    private static readonly byte[] CredentialDomain = "moongate/credential/v1"u8.ToArray();
    private static readonly byte[] HandoffDomain = "moongate/handoff/v1"u8.ToArray();

    private readonly byte[] _secret;

    public HandoffProofService(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        if (secret.Length < CredentialKeySize)
        {
            throw new ArgumentException("The handoff secret must contain at least 32 bytes.", nameof(secret));
        }

        _secret = secret.ToArray();
    }

    public byte[] DeriveCredentialKey(string username, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(password);

        var usernameBytes = Encoding.UTF8.GetBytes(username);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var input = new byte[CredentialDomain.Length + LengthPrefixSize + usernameBytes.Length +
                             LengthPrefixSize + passwordBytes.Length];

        try
        {
            var offset = 0;
            CredentialDomain.CopyTo(input, offset);
            offset += CredentialDomain.Length;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), usernameBytes.Length);
            offset += LengthPrefixSize;
            usernameBytes.CopyTo(input, offset);
            offset += usernameBytes.Length;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), passwordBytes.Length);
            offset += LengthPrefixSize;
            passwordBytes.CopyTo(input, offset);

            return HMACSHA256.HashData(_secret, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(input);
        }
    }

    public byte[] Sign(ReadOnlySpan<byte> credentialKey, string realmId, uint authKey)
    {
        if (credentialKey.Length != CredentialKeySize)
        {
            throw new ArgumentException("The credential key must contain 32 bytes.", nameof(credentialKey));
        }

        ArgumentException.ThrowIfNullOrEmpty(realmId);

        var realmBytes = Encoding.UTF8.GetBytes(realmId);
        var input = new byte[HandoffDomain.Length + LengthPrefixSize + realmBytes.Length + RedirectKeySize];

        try
        {
            var offset = 0;
            HandoffDomain.CopyTo(input, offset);
            offset += HandoffDomain.Length;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), realmBytes.Length);
            offset += LengthPrefixSize;
            realmBytes.CopyTo(input, offset);
            offset += realmBytes.Length;
            BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(offset), authKey);

            return HMACSHA256.HashData(credentialKey, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    public bool Verify(string username, string password, string realmId, uint authKey, ReadOnlySpan<byte> expected)
    {
        if (expected.Length != CredentialKeySize || string.IsNullOrEmpty(username) ||
            string.IsNullOrEmpty(realmId))
        {
            return false;
        }

        var credentialKey = DeriveCredentialKey(username, password);

        try
        {
            var actual = Sign(credentialKey, realmId, authKey);

            try
            {
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialKey);
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_secret);
    }
}
