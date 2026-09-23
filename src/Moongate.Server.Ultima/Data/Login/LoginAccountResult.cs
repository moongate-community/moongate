using System.Collections.ObjectModel;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Data.Login;

/// <summary>Verified account identity and its single eligible realm snapshot.</summary>
public sealed class LoginAccountResult
{
    public bool Success => DenialReason is null;

    public LoginDeniedReason? DenialReason { get; }

    public Serial AccountId { get; }

    public AccountType AccountType { get; }

    public IReadOnlyList<GameServerEntry> Servers { get; }

    public LoginAccountResult(LoginDeniedReason reason)
    {
        DenialReason = reason;
        Servers = Array.Empty<GameServerEntry>();
    }

    public LoginAccountResult(Serial accountId, AccountType accountType, IEnumerable<GameServerEntry> servers)
    {
        AccountId = accountId;
        AccountType = accountType;
        Servers = new ReadOnlyCollection<GameServerEntry>(servers.ToArray());
    }
}
