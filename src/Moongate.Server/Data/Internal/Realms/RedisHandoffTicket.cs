using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Data.Internal.Realms;

/// <summary>
///     Serialized Redis value for one pending login-to-game redirect.
/// </summary>
internal sealed record RedisHandoffTicket(
    uint AccountId,
    AccountType AccountType,
    string Username,
    string RealmId,
    Guid InstanceId,
    string? ClientVersion,
    byte[] Proof
);
