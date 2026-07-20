using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using SquidStd.Services.Core.Services;
using Xunit;

namespace Moongate.Tests.Server.Services.Accounts;

public sealed class AccountRegistrationTests
{
    private static (AccountService accounts, EventBusService bus) Create()
    {
        var persistence = new FakePersistenceService();
        var sessions = new StubSessionManager();
        var bus = new EventBusService();
        var characters = CharacterServiceFixture.Create(persistence, bus, sessions);

        return (new AccountService(persistence, characters, sessions, bus), bus);
    }

    [Fact]
    public void RegisterPending_CreatesInactiveAccountWithToken()
    {
        var (accounts, _) = Create();

        var result = accounts.RegisterPending("newbie", "secret", "new@bie.test");

        Assert.Equal(AccountRegisterResultType.Created, result.Result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        var account = accounts.GetByUsername("newbie");
        Assert.NotNull(account);
        Assert.False(account!.IsActive);
        Assert.Equal(result.Token, account.ActivationToken);
    }

    [Fact]
    public void RegisterPending_PublishesEvent()
    {
        var (accounts, bus) = Create();
        AccountRegistrationRequestedEvent? seen = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (e, _) =>
            {
                seen = e;

                return Task.CompletedTask;
            }
        );

        var result = accounts.RegisterPending("newbie", "secret", "new@bie.test");

        Assert.NotNull(seen);
        Assert.Equal("newbie", seen!.Username);
        Assert.Equal(result.Token, seen.Token);
    }

    [Theory]
    [InlineData("", "p", "e@e.test", AccountRegisterResultType.UsernameEmpty)]
    [InlineData("u", "", "e@e.test", AccountRegisterResultType.PasswordEmpty)]
    [InlineData("u", "p", "", AccountRegisterResultType.EmailEmpty)]
    [InlineData("u", "p", "not-an-email", AccountRegisterResultType.EmailInvalid)]
    public void RegisterPending_Validates(string user, string pass, string email, AccountRegisterResultType expected)
    {
        var (accounts, _) = Create();

        Assert.Equal(expected, accounts.RegisterPending(user, pass, email).Result);
    }

    [Fact]
    public void RegisterPending_UsernameTaken()
    {
        var (accounts, _) = Create();
        accounts.Create("taken", "pw", null, AccountLevelType.Player);

        Assert.Equal(
            AccountRegisterResultType.UsernameTaken,
            accounts.RegisterPending("taken", "pw", "e@e.test").Result
        );
    }

    [Fact]
    public void VerifyEmail_ActivatesAndIsSingleUse()
    {
        var (accounts, _) = Create();
        var token = accounts.RegisterPending("newbie", "secret", "new@bie.test").Token!;

        Assert.Equal(AccountVerifyResultType.Verified, accounts.VerifyEmail(token));
        Assert.True(accounts.GetByUsername("newbie")!.IsActive);

        // consumed token no longer matches
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail(token));
    }

    [Fact]
    public void VerifyEmail_UnknownOrBlankToken_IsInvalid()
    {
        var (accounts, _) = Create();

        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("nope"));
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("  "));
    }
}
