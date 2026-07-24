using System.Security.Cryptography;
using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Serilog;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Utils;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Accounts;

public class AccountService : IAccountService
{
    private readonly ILogger _logger = Log.ForContext<AccountService>();

    private readonly IEntityStore<AccountEntity, Serial> _accountStore;
    private readonly ICharacterService _characterService;
    private readonly ISessionManager _sessions;
    private readonly IEventBus _eventBus;

    public AccountService(
        IPersistenceService persistenceService,
        ICharacterService characterService,
        ISessionManager sessions,
        IEventBus eventBus
    )
    {
        _accountStore = persistenceService.GetStore<AccountEntity, Serial>();
        _characterService = characterService;
        _sessions = sessions;
        _eventBus = eventBus;
    }

    public AccountAuthResult Authenticate(string username, string password)
    {
        var account = _accountStore
            .Query()
            .FirstOrDefault(s => s.Username == username && HashUtils.VerifyPassword(password, s.PasswordHash));

        if (account == null)
        {
            return new() { Success = false, Reason = LoginDeniedReasonType.BadCredentials };
        }

        if (!account.IsActive)
        {
            return new() { Success = false, Reason = LoginDeniedReasonType.AccountBlocked };
        }

        return new() { Success = true, Username = account.Username };
    }

    public AccountCreateResultType Create(string username, string password, string? email, AccountLevelType level)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return AccountCreateResultType.UsernameEmpty;
        }

        if (string.IsNullOrEmpty(password))
        {
            return AccountCreateResultType.PasswordEmpty;
        }

        if (GetByUsername(username) is not null)
        {
            return AccountCreateResultType.UsernameTaken;
        }

        var account = new AccountEntity
        {
            Username = username,
            Email = email,
            PasswordHash = HashUtils.HashPassword(password),
            IsActive = true,
            AccountLevel = level
        };

        _accountStore.UpsertAsync(account).WaitSync();

        _logger.Information("Account created: {Username} at level {Level}", username, level);

        return AccountCreateResultType.Created;
    }

    public AccountRegisterResult RegisterPending(string username, string password, string email)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new() { Result = AccountRegisterResultType.UsernameEmpty };
        }

        if (string.IsNullOrEmpty(password))
        {
            return new() { Result = AccountRegisterResultType.PasswordEmpty };
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return new() { Result = AccountRegisterResultType.EmailEmpty };
        }

        if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
        {
            return new() { Result = AccountRegisterResultType.EmailInvalid };
        }

        if (GetByUsername(username) is not null)
        {
            return new() { Result = AccountRegisterResultType.UsernameTaken };
        }

        var token = RandomNumberGenerator.GetHexString(64);

        var account = new AccountEntity
        {
            Username = username,
            Email = email,
            PasswordHash = HashUtils.HashPassword(password),
            IsActive = false,
            ActivationToken = token,
            AccountLevel = AccountLevelType.Player
        };

        _accountStore.UpsertAsync(account).WaitSync();

        _logger.Information("Web registration pending for {Username}; awaiting email verification", username);
        _eventBus.Publish(new AccountRegistrationRequestedEvent(account.Id, username, email, token));

        return new() { Result = AccountRegisterResultType.Created, Token = token };
    }

    public AccountVerifyResultType VerifyEmail(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return AccountVerifyResultType.InvalidToken;
        }

        var account = _accountStore
            .Query()
            .FirstOrDefault(a => !string.IsNullOrEmpty(a.ActivationToken) && a.ActivationToken == token);

        if (account is null)
        {
            return AccountVerifyResultType.InvalidToken;
        }

        account.IsActive = true;
        account.ActivationToken = string.Empty;
        _accountStore.UpsertAsync(account).WaitSync();

        _logger.Information("Account {Username} verified and activated", account.Username);

        return AccountVerifyResultType.Verified;
    }

    public AccountDeleteResultType Delete(string username)
    {
        if (GetByUsername(username) is not { } account)
        {
            return AccountDeleteResultType.NotFound;
        }

        var characterCount = _characterService.GetPlayerCharacters(account.Id).Count;

        // Asked of the whole account before anything is deleted: refusing halfway would leave it
        // stripped of every character but the one still being played.
        if (_characterService.GetPlayerCharacters(account.Id).Any(character => _sessions.IsCharacterPlayed(character.Id)))
        {
            return AccountDeleteResultType.CharacterBeingPlayed;
        }

        // Always slot 0: each delete unlinks its character, so the list closes up behind it. The
        // character service owns the cascade into equipment and containers — this walks it, it does
        // not reimplement it.
        for (var remaining = characterCount; remaining > 0; remaining--)
        {
            _characterService.DeleteCharacter(account.Id, 0);
        }

        _accountStore.RemoveAsync(account.Id).WaitSync();

        _logger.Information(
            "Account deleted: {Username} (serial {Serial}) with {Count} characters",
            username,
            account.Id,
            characterCount
        );

        return AccountDeleteResultType.Deleted;
    }

    public Serial? GetAccountIdByUsername(string username)
        => GetByUsername(username)?.Id;

    public IReadOnlyList<AccountEntity> GetAll()
        => [.. _accountStore.GetAll()];

    public AccountEntity? GetById(Serial accountId)
        => _accountStore.GetById(accountId);

    public AccountEntity? GetByUsername(string username)
        => _accountStore.Query().FirstOrDefault(account => account.Username == username);

    public IReadOnlyList<string> GetUsernames()
        => _accountStore.Query().Select(account => account.Username).ToList();

    public bool SetActive(string username, bool isActive)
    {
        if (GetByUsername(username) is not { } account)
        {
            return false;
        }

        account.IsActive = isActive;
        _accountStore.UpsertAsync(account).WaitSync();

        _logger.Information("Account {Username} is now {State}", username, isActive ? "active" : "blocked");

        return true;
    }

    public bool SetLevel(string username, AccountLevelType level)
    {
        if (GetByUsername(username) is not { } account)
        {
            return false;
        }

        account.AccountLevel = level;
        _accountStore.UpsertAsync(account).WaitSync();

        _logger.Information("Account {Username} set to level {Level}", username, level);

        return true;
    }

    public bool SetPassword(string username, string password)
    {
        if (string.IsNullOrEmpty(password) || GetByUsername(username) is not { } account)
        {
            return false;
        }

        account.PasswordHash = HashUtils.HashPassword(password);
        _accountStore.UpsertAsync(account).WaitSync();

        _logger.Information("Password changed for account {Username}", username);

        return true;
    }
}
