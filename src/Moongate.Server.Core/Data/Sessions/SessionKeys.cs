using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Sessions;

/// <summary>
///     The keys of the values every <see cref="GameSession" /> carries. A new value is one more line here; a plugin
///     declares its own <see cref="SessionKey{T}" /> the same way and needs no change to the core.
/// </summary>
public static class SessionKeys
{
    public static readonly SessionKey<Serial> AccountId = new("AccountId");

    public static readonly SessionKey<Serial> CharacterId = new("CharacterId");

    public static readonly SessionKey<AccountType> AccountType =
        new("AccountType");

    public static readonly SessionKey<Version> ClientVersion =
        new("ClientVersion");
}
