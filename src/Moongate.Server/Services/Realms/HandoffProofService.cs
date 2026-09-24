using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Realms;

/// <summary>Creates domain-separated HMAC proofs for the login-to-game handoff.</summary>
public sealed class HandoffProofService : IHandoffProofService, IDisposable
{
    private const int CredentialKeySize = 32;
    private const int LengthPrefixSize = sizeof(int);
    private const int RedirectKeySize = sizeof(uint);
    private const int AccountIdSize = sizeof(uint);
    private const int AccountTypeSize = sizeof(int);
    private const int InstanceIdSize = 16;

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

    public byte[] Sign(ReadOnlySpan<byte> credentialKey, PendingHandoff handoff, uint authKey)
    {
        if (credentialKey.Length != CredentialKeySize)
        {
            throw new ArgumentException("The credential key must contain 32 bytes.", nameof(credentialKey));
        }

        ArgumentNullException.ThrowIfNull(handoff);

        if (!IsValid(handoff))
        {
            throw new ArgumentException("The pending handoff must identify an account and realm instance.",
                nameof(handoff));
        }

        var usernameBytes = Encoding.UTF8.GetBytes(handoff.Username);
        var realmBytes = Encoding.UTF8.GetBytes(handoff.RealmId);
        var versionBytes = handoff.ClientVersion is null ? null : Encoding.UTF8.GetBytes(handoff.ClientVersion);
        var input = new byte[HandoffDomain.Length + AccountIdSize + AccountTypeSize +
                             LengthPrefixSize + usernameBytes.Length + LengthPrefixSize + realmBytes.Length +
                             InstanceIdSize + LengthPrefixSize + (versionBytes?.Length ?? 0) + RedirectKeySize];

        try
        {
            var offset = 0;
            HandoffDomain.CopyTo(input, offset);
            offset += HandoffDomain.Length;
            BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(offset), handoff.AccountId.Value);
            offset += AccountIdSize;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), (int)handoff.AccountType);
            offset += AccountTypeSize;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), usernameBytes.Length);
            offset += LengthPrefixSize;
            usernameBytes.CopyTo(input, offset);
            offset += usernameBytes.Length;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), realmBytes.Length);
            offset += LengthPrefixSize;
            realmBytes.CopyTo(input, offset);
            offset += realmBytes.Length;
            handoff.InstanceId.TryWriteBytes(input.AsSpan(offset, InstanceIdSize));
            offset += InstanceIdSize;
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), versionBytes?.Length ?? -1);
            offset += LengthPrefixSize;

            if (versionBytes is not null)
            {
                versionBytes.CopyTo(input, offset);
                offset += versionBytes.Length;
            }

            BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(offset), authKey);

            return HMACSHA256.HashData(credentialKey, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    public bool Verify(string username, string password, PendingHandoff handoff, uint authKey,
        ReadOnlySpan<byte> expected)
    {
        if (expected.Length != CredentialKeySize || string.IsNullOrEmpty(username) ||
            handoff is null || !IsValid(handoff) ||
            !StringComparer.Ordinal.Equals(username, handoff.Username))
        {
            return false;
        }

        var credentialKey = DeriveCredentialKey(username, password);

        try
        {
            var actual = Sign(credentialKey, handoff, authKey);

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

    private static bool IsValid(PendingHandoff handoff)
        => handoff.AccountId.IsValid && Enum.IsDefined(handoff.AccountType) &&
           !string.IsNullOrEmpty(handoff.Username) && !string.IsNullOrEmpty(handoff.RealmId) &&
           handoff.InstanceId != Guid.Empty;

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_secret);
    }
}
