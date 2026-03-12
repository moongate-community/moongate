using System.Net.Sockets;
using Moongate.Core.Utils;
using Moongate.Network.Client;
using Moongate.Server.Commands.Accounts;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Accounts;

public sealed class PasswordCommandTests
{
    private sealed class PasswordCommandTestAccountService : IAccountService
    {
        public List<UOAccountEntity> Accounts { get; } = [];

        public Task<bool> CheckAccountExistsAsync(string username)
            => Task.FromResult(
                Accounts.Any(account => string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase))
            );

        public Task<UOAccountEntity?> CreateAccountAsync(
            string username,
            string password,
            string email = "",
            AccountType accountType = AccountType.Regular
        )
            => Task.FromResult<UOAccountEntity?>(null);

        public Task<bool> DeleteAccountAsync(Serial accountId)
            => Task.FromResult(false);

        public Task<UOAccountEntity?> GetAccountAsync(Serial accountId)
            => Task.FromResult(Accounts.FirstOrDefault(account => account.Id == accountId));

        public Task<IReadOnlyList<UOAccountEntity>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.FromResult<IReadOnlyList<UOAccountEntity>>(Accounts);
        }

        public Task<UOAccountEntity?> LoginAsync(string username, string password)
            => Task.FromResult<UOAccountEntity?>(null);

        public Task<UOAccountEntity?> UpdateAccountAsync(
            Serial accountId,
            string? username = null,
            string? password = null,
            string? email = null,
            AccountType? accountType = null,
            bool? isLocked = null,
            bool clearRecoveryCode = false,
            CancellationToken cancellationToken = default
        )
        {
            _ = username;
            _ = email;
            _ = accountType;
            _ = isLocked;
            _ = clearRecoveryCode;
            _ = cancellationToken;

            var account = Accounts.FirstOrDefault(current => current.Id == accountId);

            if (account is null)
            {
                return Task.FromResult<UOAccountEntity?>(null);
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                account.PasswordHash = HashUtils.HashPassword(password);
            }

            return Task.FromResult<UOAccountEntity?>(account);
        }
    }

    private sealed class PasswordCommandTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.Values.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(current => current.CharacterId == characterId)!;

            return session is not null;
        }

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenRegularInGame_ShouldChangeOwnPassword()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x00000010u,
            AccountType = AccountType.Regular
        };
        var sessionService = new PasswordCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var accountService = new PasswordCommandTestAccountService();
        accountService.Accounts.Add(
            new()
            {
                Id = session.AccountId,
                Username = "player",
                PasswordHash = HashUtils.HashPassword("old-password"),
                AccountType = AccountType.Regular
            }
        );
        var command = new PasswordCommand(accountService, sessionService);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "password new-password",
            ["new-password"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(HashUtils.VerifyPassword("new-password", accountService.Accounts[0].PasswordHash), Is.True);
                Assert.That(output[^1], Is.EqualTo("Password updated for account 'player'."));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenRegularInGamePassesUsername_ShouldRejectCommand()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x00000010u,
            AccountType = AccountType.Regular
        };
        var sessionService = new PasswordCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var accountService = new PasswordCommandTestAccountService();
        var command = new PasswordCommand(accountService, sessionService);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "password other new-password",
            ["other", "new-password"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(accountService.Accounts, Is.Empty);
                Assert.That(output[^1], Is.EqualTo("Usage: .password <newPassword>"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenAdministratorUsesConsole_ShouldChangeTargetPassword()
    {
        var sessionService = new PasswordCommandTestGameNetworkSessionService();
        var accountService = new PasswordCommandTestAccountService();
        accountService.Accounts.Add(
            new()
            {
                Id = (Serial)0x00000020u,
                Username = "target",
                PasswordHash = HashUtils.HashPassword("old-password"),
                AccountType = AccountType.Regular
            }
        );
        var command = new PasswordCommand(accountService, sessionService);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "password target new-password",
            ["target", "new-password"],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(HashUtils.VerifyPassword("new-password", accountService.Accounts[0].PasswordHash), Is.True);
                Assert.That(output[^1], Is.EqualTo("Password updated for account 'target'."));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenConsoleOmitsUsername_ShouldPrintConsoleUsage()
    {
        var command = new PasswordCommand(
            new PasswordCommandTestAccountService(),
            new PasswordCommandTestGameNetworkSessionService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "password new-password",
            ["new-password"],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: password <username> <newPassword>"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTargetAccountDoesNotExist_ShouldPrintError()
    {
        var command = new PasswordCommand(
            new PasswordCommandTestAccountService(),
            new PasswordCommandTestGameNetworkSessionService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "password missing new-password",
            ["missing", "new-password"],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Account 'missing' was not found."));
    }
}
