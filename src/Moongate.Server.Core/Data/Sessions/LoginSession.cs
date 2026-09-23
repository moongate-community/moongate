using Moongate.Core.Primitives;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Sessions;

/// <summary>Login-only connection state with a generation independent of the numeric session slot.</summary>
public sealed class LoginSession
{
    private readonly Lock _gate = new();
    private Serial _accountId;
    private AccountType _accountType = AccountType.Regular;
    private bool _disconnected;

    public NetworkSession NetworkSession { get; }

    public long SessionId => NetworkSession.SessionId;

    public Guid Generation { get; } = Guid.NewGuid();

    public Serial AccountId
    {
        get { lock (_gate) { return _accountId; } }
    }

    public AccountType AccountType
    {
        get { lock (_gate) { return _accountType; } }
    }

    public bool IsDisconnected
    {
        get { lock (_gate) { return _disconnected; } }
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
            return true;
        }
    }

    public void ClearAccount()
    {
        lock (_gate)
        {
            _accountId = Serial.Zero;
            _accountType = AccountType.Regular;
        }
    }

    public void Disconnect()
    {
        lock (_gate)
        {
            _disconnected = true;
            _accountId = Serial.Zero;
            NetworkSession.DetachClient();
        }
    }
}
