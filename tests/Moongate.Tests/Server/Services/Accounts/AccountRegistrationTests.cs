using System.Security.Cryptography;
using System.Text;
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
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string, AccountRegisterResultType> PublicRegistrationInputs()
        => new()
        {
            { "ab", "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "abc", "secret99", "new@bie.test", AccountRegisterResultType.Created },
            { new string('a', 30), "secret99", "new@bie.test", AccountRegisterResultType.Created },
            { new string('a', 31), "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "naïve", "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "new!bie", "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "newbie", "1234567", "new@bie.test", AccountRegisterResultType.PasswordInvalid },
            { "newbie", "12345678", "new@bie.test", AccountRegisterResultType.Created },
            { "newbie", new string('p', 30), "new@bie.test", AccountRegisterResultType.Created },
            { "newbie", new string('p', 31), "new@bie.test", AccountRegisterResultType.PasswordInvalid },
            { "newbie", "secret\n9", "new@bie.test", AccountRegisterResultType.PasswordInvalid },
            { "newbie", "pässword", "new@bie.test", AccountRegisterResultType.PasswordInvalid }
        };

    private static (
        AccountService Accounts,
        EventBusService Bus,
        FakePersistenceService Persistence,
        CharacterService Characters,
        StubSessionManager Sessions
    ) Create(TimeProvider? timeProvider = null)
    {
        var persistence = new FakePersistenceService();
        var sessions = new StubSessionManager();
        var bus = new EventBusService();
        var characters = CharacterServiceFixture.Create(persistence, bus, sessions);

        return (
            new AccountService(persistence, characters, sessions, bus, timeProvider ?? new FixedTimeProvider(Now)),
            bus,
            persistence,
            characters,
            sessions
        );
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [Fact]
    public void ResendVerification_RotatesTokenAndPublishesOneEvent()
    {
        var (accounts, bus, _, _, _) = Create();
        var first = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        AccountRegistrationRequestedEvent? resent = null;
        var eventCount = 0;
        bus.Subscribe<AccountRegistrationRequestedEvent>((message, _) =>
            {
                resent = message;
                eventCount++;
                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "NEW@BIE.TEST");

        Assert.Equal(AccountResendResultType.Sent, result);
        Assert.NotNull(resent);
        Assert.Equal(1, eventCount);
        Assert.NotEqual(first, resent!.Token);
        Assert.Equal(Hash(resent.Token), accounts.GetByUsername("newbie")!.ActivationTokenHash);
    }

    [Fact]
    public void ResendVerification_MissingAccount_IsIgnoredWithoutPublishingAnEvent()
    {
        var (accounts, bus, _, _, _) = Create();
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>((message, _) =>
            {
                resent = message;
                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Null(resent);
        Assert.Null(accounts.GetByUsername("newbie"));
    }

    [Fact]
    public void ResendVerification_ActiveAccount_IsIgnoredWithoutPublishingAnEvent()
    {
        var (accounts, bus, _, _, _) = Create();
        accounts.Create("newbie", "secret99", "new@bie.test", AccountLevelType.Player);
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>((message, _) =>
            {
                resent = message;
                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Null(resent);
        Assert.True(accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public void ResendVerification_MismatchedEmail_IsIgnoredWithoutChangingTokenOrPublishingAnEvent()
    {
        var (accounts, bus, _, _, _) = Create();
        var first = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>((message, _) =>
            {
                resent = message;
                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "other@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Null(resent);
        Assert.Equal(Hash(first), accounts.GetByUsername("newbie")!.ActivationTokenHash);
    }

    [Theory]
    [InlineData("ab", "new@bie.test", AccountResendResultType.UsernameInvalid)]
    [InlineData("newbie", "not-an-email", AccountResendResultType.EmailInvalid)]
    public void ResendVerification_InvalidInput_ReturnsValidationResultWithoutPublishingAnEvent(
        string username,
        string email,
        AccountResendResultType expected
    )
    {
        var (accounts, bus, _, _, _) = Create();
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>((message, _) =>
            {
                resent = message;
                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification(username, email);

        Assert.Equal(expected, result);
        Assert.Null(resent);
    }

    [Fact]
    public void ResendVerification_LegacyPendingAccount_MigratesToHashedToken()
    {
        var (accounts, bus, _, _, _) = Create();
        accounts.Create("newbie", "secret99", "new@bie.test", AccountLevelType.Player);
        var account = accounts.GetByUsername("newbie")!;
        account.IsActive = false;
        account.ActivationToken = "legacy-token";
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>((message, _) =>
            {
                resent = message;
                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Sent, result);
        Assert.NotNull(resent);
        Assert.Empty(account.ActivationToken);
        Assert.Equal(Hash(resent!.Token), account.ActivationTokenHash);
    }

    [Fact]
    public void ResendVerification_ResetsExpiryToTwentyFourHoursFromCurrentTime()
    {
        var (accounts, bus, persistence, characters, sessions) = Create();
        accounts.RegisterPending("newbie", "secret99", "new@bie.test");
        var later = Now.AddHours(1);
        var resendingAccounts = new AccountService(
            persistence,
            characters,
            sessions,
            bus,
            new FixedTimeProvider(later)
        );

        var result = resendingAccounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Sent, result);
        Assert.Equal(later.AddHours(24), resendingAccounts.GetByUsername("newbie")!.ActivationTokenExpiresAtUtc);
    }

    [Fact]
    public void RegisterPending_StoresOnlyAHashAndA24HourExpiry()
    {
        var (accounts, _, _, _, _) = Create();

        var result = accounts.RegisterPending("newbie", "secret99", "new@bie.test");

        Assert.Equal(AccountRegisterResultType.Created, result.Result);
        Assert.NotNull(result.Token);
        Assert.Matches("^[0-9A-Fa-f]{64}$", result.Token);
        var account = accounts.GetByUsername("newbie");
        Assert.NotNull(account);
        Assert.False(account!.IsActive);
        Assert.Empty(account.ActivationToken);
        Assert.NotEqual(result.Token, account.ActivationTokenHash);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Token))),
            account.ActivationTokenHash
        );
        Assert.Matches("^[0-9A-F]{64}$", account.ActivationTokenHash);
        Assert.Equal(Now.AddHours(24), account.ActivationTokenExpiresAtUtc);
    }

    [Fact]
    public void RegisterPending_PublishesEvent()
    {
        var (accounts, bus, _, _, _) = Create();
        AccountRegistrationRequestedEvent? seen = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>((e, _) =>
            {
                seen = e;

                return Task.CompletedTask;
            }
        );

        var result = accounts.RegisterPending("newbie", "secret99", "new@bie.test");

        Assert.NotNull(seen);
        Assert.Equal("newbie", seen!.Username);
        Assert.Equal(result.Token, seen.Token);
    }

    [Theory, MemberData(nameof(PublicRegistrationInputs))]
    public void RegisterPending_PublicCredentialRules_ReturnsExpectedResult(
        string username,
        string password,
        string email,
        AccountRegisterResultType expected
    )
    {
        var (accounts, _, _, _, _) = Create();

        Assert.Equal(expected, accounts.RegisterPending(username, password, email).Result);
    }

    [Theory]
    [InlineData("", "secret99", "new@bie.test", AccountRegisterResultType.UsernameEmpty)]
    [InlineData("newbie", "", "new@bie.test", AccountRegisterResultType.PasswordEmpty)]
    [InlineData("newbie", "secret99", "", AccountRegisterResultType.EmailEmpty)]
    [InlineData("newbie", "secret99", "not-an-email", AccountRegisterResultType.EmailInvalid)]
    public void RegisterPending_MissingOrInvalidInputs_ReturnsExpectedResult(
        string username,
        string password,
        string email,
        AccountRegisterResultType expected
    )
    {
        var (accounts, _, _, _, _) = Create();

        Assert.Equal(expected, accounts.RegisterPending(username, password, email).Result);
    }

    [Fact]
    public void RegisterPending_TrimmedUsernameAndEmail_StoresNormalizedValues()
    {
        var (accounts, _, _, _, _) = Create();

        var result = accounts.RegisterPending(" newbie ", "secret99", " new@bie.test ");
        var account = accounts.GetByUsername("newbie");

        Assert.Equal(AccountRegisterResultType.Created, result.Result);
        Assert.NotNull(account);
        Assert.Equal("newbie", account!.Username);
        Assert.Equal("new@bie.test", account.Email);
    }

    [Fact]
    public void RegisterPending_PreservesPasswordWhitespace()
    {
        var (accounts, _, _, _, _) = Create();
        var password = " secret99 ";
        var token = accounts.RegisterPending("newbie", password, "new@bie.test").Token!;

        Assert.Equal(AccountVerifyResultType.Verified, accounts.VerifyEmail(token));
        Assert.True(accounts.Authenticate("newbie", password).Success);
        Assert.False(accounts.Authenticate("newbie", password.Trim()).Success);
    }

    [Fact]
    public void RegisterPending_UsernameTaken()
    {
        var (accounts, _, _, _, _) = Create();
        accounts.Create("taken", "pw", null, AccountLevelType.Player);

        Assert.Equal(
            AccountRegisterResultType.UsernameTaken,
            accounts.RegisterPending("taken", "secret99", "e@e.test").Result
        );
    }

    [Fact]
    public void RegisterPending_EmailTakenForActiveAndInactiveAccounts()
    {
        var (accounts, _, _, _, _) = Create();
        accounts.Create("active", "pw", "taken@example.test", AccountLevelType.Player);
        Assert.Equal(
            AccountRegisterResultType.EmailTaken,
            accounts.RegisterPending("another", "secret99", "TAKEN@example.test").Result
        );

        Assert.Equal(
            AccountRegisterResultType.Created,
            accounts.RegisterPending("inactive", "secret99", "inactive@example.test").Result
        );
        Assert.Equal(
            AccountRegisterResultType.EmailTaken,
            accounts.RegisterPending("other", "secret99", "INACTIVE@example.test").Result
        );
    }

    [Fact]
    public void VerifyEmail_BeforeExpiry_ActivatesAndIsSingleUse()
    {
        var (accounts, _, _, _, _) = Create();
        var token = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;

        Assert.Equal(AccountVerifyResultType.Verified, accounts.VerifyEmail(token));
        Assert.True(accounts.GetByUsername("newbie")!.IsActive);
        Assert.Empty(accounts.GetByUsername("newbie")!.ActivationTokenHash);
        Assert.Null(accounts.GetByUsername("newbie")!.ActivationTokenExpiresAtUtc);

        // consumed token no longer matches
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail(token));
    }

    [Fact]
    public void VerifyEmail_AtExpiry_ReturnsExpiredTokenAndClearsTokenState()
    {
        var (accounts, bus, persistence, characters, sessions) = Create();
        var token = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        var atExpiry = new AccountService(
            persistence,
            characters,
            sessions,
            bus,
            new FixedTimeProvider(Now.AddHours(24))
        );

        Assert.Equal(AccountVerifyResultType.ExpiredToken, atExpiry.VerifyEmail(token));
        var account = atExpiry.GetByUsername("newbie")!;
        Assert.False(account.IsActive);
        Assert.Empty(account.ActivationToken);
        Assert.Empty(account.ActivationTokenHash);
        Assert.Null(account.ActivationTokenExpiresAtUtc);
    }

    [Fact]
    public void VerifyEmail_LegacyPlaintextToken_ReturnsExpiredTokenAndClearsLegacyState()
    {
        var (accounts, _, _, _, _) = Create();
        var account = accounts.GetByUsername("newbie");
        Assert.Null(account);
        accounts.Create("newbie", "secret99", "new@bie.test", AccountLevelType.Player);
        account = accounts.GetByUsername("newbie")!;
        account.IsActive = false;
        account.ActivationToken = "legacy-token";

        Assert.Equal(AccountVerifyResultType.ExpiredToken, accounts.VerifyEmail("legacy-token"));
        Assert.False(accounts.GetByUsername("newbie")!.IsActive);
        Assert.Empty(accounts.GetByUsername("newbie")!.ActivationToken);
    }

    [Fact]
    public void VerifyEmail_UnknownOrBlankToken_IsInvalid()
    {
        var (accounts, _, _, _, _) = Create();

        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("nope"));
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("  "));
    }
}
