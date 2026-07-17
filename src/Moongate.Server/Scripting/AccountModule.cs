using Moongate.Core.Interfaces;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Serilog;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes account creation and management to Lua. Accounts are referenced by username — the handle a
/// player logs in with — rather than by serial, which no operator ever has to hand.
/// Only <c>account.delete</c> reaches into world state (it deletes the account's characters and what
/// they carry) and so warns when run off the game-loop thread; the rest touch the account store alone.
/// </summary>
[ScriptModule("account", "Create and manage accounts by username.")]
public sealed class AccountModule
{
    private readonly ILogger _logger = Log.ForContext<AccountModule>();

    private readonly IAccountService _accounts;
    private readonly ILoopThread _loopThread;

    public AccountModule(IAccountService accounts, ILoopThread loopThread)
    {
        _accounts = accounts;
        _loopThread = loopThread;
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

    [ScriptFunction("get", "Returns a field table for the account, or nil.")]
    public Dictionary<string, object?>? Get(string username)
    {
        if (_accounts.GetByUsername(username) is not { } account)
        {
            return null;
        }

        return new()
        {
            ["id"] = account.Id.Value,
            ["username"] = account.Username,
            ["email"] = account.Email,
            ["level"] = account.AccountLevel.ToString(),
            ["is_active"] = account.IsActive,
            ["mobiles"] = account.MobileIds.Select(id => id.Value).ToList()
        };
    }

    [ScriptFunction("list", "Returns every account's username.")]
    public List<string> List()
        => [.. _accounts.GetUsernames()];

    [ScriptFunction("exists", "True when an account answers to that username.")]
    public bool Exists(string username)
        => _accounts.GetByUsername(username) is not null;

    [ScriptFunction("set_password", "Replaces the account's password; false on unknown username.")]
    public bool SetPassword(string username, string password)
        => _accounts.SetPassword(username, password);

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

    [ScriptFunction("set_active", "Activates or blocks the account; false on unknown username.")]
    public bool SetActive(string username, bool isActive)
        => _accounts.SetActive(username, isActive);

    [ScriptFunction("delete", "Deletes the account and its characters; false when refused.")]
    public bool Delete(string username)
    {
        LoopGuard.Warn(_loopThread, "account.delete");

        var result = _accounts.Delete(username);

        if (result != AccountDeleteResultType.Deleted)
        {
            _logger.Warning("account.delete refused for {Username}: {Reason}", username, result);
        }

        return result == AccountDeleteResultType.Deleted;
    }
}
