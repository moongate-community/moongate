using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Interfaces.Accounts;

/// <summary>
/// Authenticates and manages accounts. An account is addressed by its username: that is the handle a
/// player logs in with and an operator knows it by, and it is unique across the store.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Checks the credentials against the store. Denies unknown usernames, wrong passwords and
    /// deactivated accounts alike.
    /// </summary>
    AccountAuthResult Authenticate(string username, string password);

    /// <summary>
    /// Creates an active account with a hashed password. Refuses a blank username or password, and a
    /// username already taken.
    /// </summary>
    AccountCreateResultType Create(string username, string password, string? email, AccountLevelType level);

    /// <summary>
    /// Creates a pending Player account for web self-registration: inactive, carrying a single-use
    /// verification token, with the (required, validated) email stored. Publishes
    /// <see cref="Moongate.Server.Abstractions.Data.Events.AccountRegistrationRequestedEvent" />. The
    /// account cannot log in until verified.
    /// </summary>
    AccountRegisterResult RegisterPending(string username, string password, string email);

    /// <summary>
    /// Activates the account holding <paramref name="token" /> and clears the token. Idempotent by
    /// construction: a consumed or unknown token matches nothing.
    /// </summary>
    AccountVerifyResultType VerifyEmail(string token);

    /// <summary>
    /// Deletes the account along with every character it owns and everything those characters carry.
    /// Refused outright while any of them is being played.
    /// </summary>
    AccountDeleteResultType Delete(string username);

    Serial? GetAccountIdByUsername(string username);

    /// <summary>
    /// Every account, in one pass over the store. Exists because <see cref="GetByUsername" /> scans:
    /// building a list from <see cref="GetUsernames" /> plus a lookup per name would scan the store once
    /// per account.
    /// </summary>
    IReadOnlyList<AccountEntity> GetAll();

    /// <summary>
    /// Returns the account with that id, or null when none has it. A direct key lookup, unlike
    /// <see cref="GetByUsername" /> which scans and clones the whole store — worth having because the
    /// bearer token carries the account id, so a route already knows it and need not search by name.
    /// </summary>
    AccountEntity? GetById(Serial accountId);

    /// <summary>Returns the account with that username, or null when none has it.</summary>
    AccountEntity? GetByUsername(string username);

    /// <summary>Returns every account's username, in store order.</summary>
    IReadOnlyList<string> GetUsernames();

    /// <summary>
    /// Activates or deactivates the account. A deactivated account is refused at login but keeps its
    /// characters. False on unknown username.
    /// </summary>
    bool SetActive(string username, bool isActive);

    /// <summary>Sets the account's privilege level. False on unknown username.</summary>
    bool SetLevel(string username, AccountLevelType level);

    /// <summary>
    /// Replaces the account's password with the hash of <paramref name="password" />. False on unknown
    /// username or blank password.
    /// </summary>
    bool SetPassword(string username, string password);
}
