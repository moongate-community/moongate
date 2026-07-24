using Moongate.Core.Types;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Scripting.Views;
using Serilog;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes account creation and management to Lua. Accounts are referenced by username — the handle a
/// player logs in with — rather than by serial, which no operator ever has to hand.
/// </summary>
[ScriptModule("account", "Create and manage accounts by username.")]
public sealed class AccountModule
{
    private readonly ILogger _logger = Log.ForContext<AccountModule>();

    private readonly IAccountService _accounts;

    public AccountModule(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [ScriptFunction("create", "Creates an active account; returns true when created.")]
    public bool Create(string username, string password, string? email, object level)
    {
        if (!ScriptEnums.TryResolve<AccountLevelType>(level, out var accountLevel))
        {
            accountLevel = AccountLevelType.Player;
        }

        var result = _accounts.Create(username, password, email, accountLevel);

        if (result != AccountCreateResultType.Created)
        {
            _logger.Warning("account.create refused for {Username}: {Reason}", username, result);
        }

        return result == AccountCreateResultType.Created;
    }

    [ScriptFunction("delete", "Deletes the account and its characters; false when refused.")]
    public bool Delete(string username)
    {
        var result = _accounts.Delete(username);

        if (result != AccountDeleteResultType.Deleted)
        {
            _logger.Warning("account.delete refused for {Username}: {Reason}", username, result);
        }

        return result == AccountDeleteResultType.Deleted;
    }

    [ScriptFunction("exists", "True when an account answers to that username.")]
    public bool Exists(string username)
        => _accounts.GetByUsername(username) is not null;

    [ScriptFunction("get", "Returns a field table for the account, or nil.")]
    public AccountLuaView? Get(string username)
        => _accounts.GetByUsername(username) is { } account ? new AccountLuaView(account) : null;

    [ScriptFunction("list", "Returns every account's username.")]
    public List<string> List()
        => [.. _accounts.GetUsernames()];

    [ScriptFunction("set_active", "Activates or blocks the account; false on unknown username.")]
    public bool SetActive(string username, bool isActive)
        => _accounts.SetActive(username, isActive);

    [ScriptFunction("set_level", "Sets the account's privilege level; false on unknown username or level.")]
    public bool SetLevel(string username, object level)
    {
        if (!ScriptEnums.TryResolve<AccountLevelType>(level, out var accountLevel))
        {
            _logger.Warning("account.set_level got an unknown level for {Username}: {Level}", username, level);

            return false;
        }

        return _accounts.SetLevel(username, accountLevel);
    }

    [ScriptFunction("set_password", "Replaces the account's password; false on unknown username.")]
    public bool SetPassword(string username, string password)
        => _accounts.SetPassword(username, password);
}
