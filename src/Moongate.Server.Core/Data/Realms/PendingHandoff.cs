using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Realms;

/// <summary>
///     Account identity and target realm accepted by the login server for one redirect.
/// </summary>
public sealed record PendingHandoff(
    Serial AccountId,
    AccountType AccountType,
    string Username,
    string RealmId,
    Guid InstanceId,
    ClientVersion? ClientVersion
);
