using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Sessions;

/// <summary>
///     Login-only connection state with a generation independent of the numeric session slot.
/// </summary>
public sealed class LoginSession
{
    private const int CredentialKeySize = 32;

    private readonly Lock _gate = new();
    private Serial _accountId;
    private AccountType _accountType = AccountType.Regular;
    private string? _username;
    private byte[]? _credentialKey;
    private bool _disconnected;

    public NetworkSession NetworkSession { get; }

    public long SessionId => NetworkSession.SessionId;

    public Guid Generation { get; } = Guid.NewGuid();

    public Serial AccountId
    {
        get
        {
            lock (_gate)
            {
                return _accountId;
            }
        }
    }

    public AccountType AccountType
    {
        get
        {
            lock (_gate)
            {
                return _accountType;
            }
        }
    }

    public bool IsDisconnected
    {
        get
        {
            lock (_gate)
            {
                return _disconnected;
            }
        }
    }

    public LoginSession(INetworkConnection connection)
    {
        NetworkSession = new(connection);
    }

    public bool TrySetAccount(Serial accountId, AccountType accountType)
    {
        lock (_gate)
        {
            if (_disconnected || !Enum.IsDefined(accountType))
            {
                return false;
            }

            _accountId = accountId;
            _accountType = accountType;
            ClearCredentialKey();

            return true;
        }
    }

    public bool TrySetAccount(
        Serial accountId,
        AccountType accountType,
        string username,
        ReadOnlySpan<byte> credentialKey
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        if (credentialKey.Length != CredentialKeySize)
        {
            throw new ArgumentException("The credential key must contain 32 bytes.", nameof(credentialKey));
        }

        lock (_gate)
        {
            if (_disconnected || !accountId.IsValid || !Enum.IsDefined(accountType))
            {
                return false;
            }

            ClearCredentialKey();
            _accountId = accountId;
            _accountType = accountType;
            _username = username;
            _credentialKey = credentialKey.ToArray();

            return true;
        }
    }

    /// <summary>
    ///     Returns an atomic account snapshot with a caller-owned key that must be cleared after use.
    /// </summary>
    public bool TryGetAuthenticatedAccount(
        out Serial accountId,
        out AccountType accountType,
        [NotNullWhen(true)] out string? username,
        [NotNullWhen(true)] out byte[]? credentialKey
    )
    {
        lock (_gate)
        {
            accountId = _accountId;
            accountType = _accountType;
            username = _username;
            credentialKey = _credentialKey?.ToArray();

            return !_disconnected && username is not null && credentialKey is not null;
        }
    }

    public void ClearAccount()
    {
        lock (_gate)
        {
            _accountId = Serial.Zero;
            _accountType = AccountType.Regular;
            ClearCredentialKey();
        }
    }

    public void Disconnect()
    {
        lock (_gate)
        {
            _disconnected = true;
            _accountId = Serial.Zero;
            _accountType = AccountType.Regular;
            ClearCredentialKey();
            NetworkSession.DetachClient();
        }
    }

    private void ClearCredentialKey()
    {
        if (_credentialKey is not null)
        {
            CryptographicOperations.ZeroMemory(_credentialKey);
            _credentialKey = null;
        }

        _username = null;
    }
}
