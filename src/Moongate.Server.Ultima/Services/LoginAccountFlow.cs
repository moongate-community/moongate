using Moongate.Network.Packets.Data.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Login;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>Authenticates an account and selects one immutable, privilege-filtered realm list.</summary>
public sealed class LoginAccountFlow
{
    private readonly IAccountService _accounts;
    private readonly IRealmDirectoryService _directory;

    public LoginAccountFlow(IAccountService accounts, IRealmDirectoryService directory)
    {
        _accounts = accounts;
        _directory = directory;
    }

    public async Task<LoginAccountResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken
    )
    {
        var account = await _accounts.LoginAsync(username, password, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return new(LoginDeniedReason.InvalidCredentials);
        }

        if (!Enum.IsDefined(account.AccountType))
        {
            return new(LoginDeniedReason.CommunicationProblem);
        }

        var servers = _directory.GetAvailable(account.AccountType)
                                .Select(
                                    realm => new GameServerEntry(
                                        realm.ServerIndex,
                                        realm.Name,
                                        0,
                                        0,
                                        realm.Address
                                    )
                                )
                                .ToArray();

        return servers.Length == 0
                   ? new(LoginDeniedReason.CommunicationProblem)
                   : new(account.Id, account.AccountType, servers);
    }
}
