using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Services.Accounts;

public sealed class AccountRegistrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LegacyPlaintextToken_PrivilegedAccountCannotMigrateOrVerify()
    {
        var (accounts, bus, _, _, _) = Create();
        accounts.Create("staff", "secret99", "staff@bie.test", AccountLevelType.Administrator);
        accounts.SetActive("staff", false);
        var staff = accounts.GetByUsername("staff")!;
        staff.ActivationToken = "legacy-staff-token";
        var eventCount = 0;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (_, _) =>
            {
                eventCount++;

                return Task.CompletedTask;
            }
        );

        Assert.Equal(AccountResendResultType.Ignored, accounts.ResendVerification("staff", "staff@bie.test"));
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("legacy-staff-token"));
        Assert.Equal("legacy-staff-token", staff.ActivationToken);
        Assert.False(staff.IsActive);
        Assert.Equal(0, eventCount);
    }

    public static TheoryData<string, string, string, AccountRegisterResultType> PublicRegistrationInputs()
        => new()
        {
            { "ab", "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "abc", "secret99", "new@bie.test", AccountRegisterResultType.Created },
            { new('a', 30), "secret99", "new@bie.test", AccountRegisterResultType.Created },
            { new('a', 31), "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "naïve", "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "new!bie", "secret99", "new@bie.test", AccountRegisterResultType.UsernameInvalid },
            { "newbie", "1234567", "new@bie.test", AccountRegisterResultType.PasswordInvalid },
            { "newbie", "12345678", "new@bie.test", AccountRegisterResultType.Created },
            { "newbie", new('p', 30), "new@bie.test", AccountRegisterResultType.Created },
            { "newbie", new('p', 31), "new@bie.test", AccountRegisterResultType.PasswordInvalid },
            { "newbie", "secret\n9", "new@bie.test", AccountRegisterResultType.PasswordInvalid },
            { "newbie", "pässword", "new@bie.test", AccountRegisterResultType.PasswordInvalid }
        };

    [Fact]
    public async Task RegisterPending_ConcurrentDuplicateEmail_CreatesOnlyOneAccount()
    {
        var (accounts, bus, _, _, _) = Create();
        var events = new ConcurrentQueue<AccountRegistrationRequestedEvent>();
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
            {
                events.Enqueue(message);

                return Task.CompletedTask;
            }
        );

        var results = await RunConcurrently(
                          () => accounts.RegisterPending("first", "secret99", "same@bie.test").Result,
                          () => accounts.RegisterPending("second", "secret99", "SAME@BIE.TEST").Result
                      );

        Assert.Equal(1, results.Count(result => result == AccountRegisterResultType.Created));
        Assert.Equal(1, results.Count(result => result == AccountRegisterResultType.EmailTaken));
        Assert.Single(accounts.GetAll());
        Assert.Single(events);
    }

    [Fact]
    public async Task RegisterPending_ConcurrentDuplicateUsername_CreatesOnlyOneAccount()
    {
        var (accounts, bus, _, _, _) = Create();
        var events = new ConcurrentQueue<AccountRegistrationRequestedEvent>();
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
            {
                events.Enqueue(message);

                return Task.CompletedTask;
            }
        );

        var results = await RunConcurrently(
                          () => accounts.RegisterPending("newbie", "secret99", "one@bie.test").Result,
                          () => accounts.RegisterPending("newbie", "secret99", "two@bie.test").Result
                      );

        Assert.Equal(1, results.Count(result => result == AccountRegisterResultType.Created));
        Assert.Equal(1, results.Count(result => result == AccountRegisterResultType.UsernameTaken));
        Assert.Single(accounts.GetAll());
        Assert.Single(events);
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

    [Theory, InlineData("", "secret99", "new@bie.test", AccountRegisterResultType.UsernameEmpty),
     InlineData("newbie", "", "new@bie.test", AccountRegisterResultType.PasswordEmpty),
     InlineData("newbie", "secret99", "", AccountRegisterResultType.EmailEmpty),
     InlineData("newbie", "secret99", "not-an-email", AccountRegisterResultType.EmailInvalid)]
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
    public void RegisterPending_PreservesPasswordWhitespace()
    {
        var (accounts, _, _, _, _) = Create();
        var password = " secret99 ";
        var token = accounts.RegisterPending("newbie", password, "new@bie.test").Token!;

        Assert.Equal(AccountVerifyResultType.Verified, accounts.VerifyEmail(token));
        Assert.True(accounts.Authenticate("newbie", password).Success);
        Assert.False(accounts.Authenticate("newbie", password.Trim()).Success);
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

    [Fact]
    public void RegisterPending_PublishesEvent()
    {
        var (accounts, bus, _, _, _) = Create();
        AccountRegistrationRequestedEvent? seen = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (e, _) =>
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
        Assert.True(account.IsPublicRegistrationPending);
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
    public void ResendVerification_ActiveAccount_IsIgnoredWithoutPublishingAnEvent()
    {
        var (accounts, bus, persistence, _, _) = Create();
        accounts.Create("newbie", "secret99", "new@bie.test", AccountLevelType.Player);
        var accountStore = persistence.Store<AccountEntity>();
        var upsertsBefore = accountStore.UpsertCount;
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
            {
                resent = message;

                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Null(resent);
        Assert.Equal(upsertsBefore, accountStore.UpsertCount);
        Assert.True(accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public void ResendVerification_BlockedPlayer_IsIgnoredWithoutRotatingOrPublishingAnEvent()
    {
        var (accounts, bus, persistence, _, _) = Create();
        accounts.Create("newbie", "secret99", "new@bie.test", AccountLevelType.Player);
        accounts.SetActive("newbie", false);
        var account = accounts.GetByUsername("newbie")!;
        account.ActivationTokenHash = Hash("blocked-token");
        account.ActivationTokenExpiresAtUtc = Now.AddHours(1);
        var originalHash = account.ActivationTokenHash;
        var accountStore = persistence.Store<AccountEntity>();
        var upsertsBefore = accountStore.UpsertCount;
        var eventCount = 0;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (_, _) =>
            {
                eventCount++;

                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Equal(0, eventCount);
        Assert.Equal(upsertsBefore, accountStore.UpsertCount);
        Assert.Equal(originalHash, account.ActivationTokenHash);
        Assert.False(account.IsActive);
    }

    [Theory, InlineData("ab", "new@bie.test", AccountResendResultType.UsernameInvalid),
     InlineData("newbie", "not-an-email", AccountResendResultType.EmailInvalid)]
    public void ResendVerification_InvalidInput_ReturnsValidationResultWithoutPublishingAnEvent(
        string username,
        string email,
        AccountResendResultType expected
    )
    {
        var (accounts, bus, _, _, _) = Create();
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
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
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
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
        Assert.True(account.IsPublicRegistrationPending);
    }

    [Fact]
    public void ResendVerification_MismatchedEmail_IsIgnoredWithoutChangingTokenOrPublishingAnEvent()
    {
        var (accounts, bus, persistence, _, _) = Create();
        var first = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        var accountStore = persistence.Store<AccountEntity>();
        var upsertsBefore = accountStore.UpsertCount;
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
            {
                resent = message;

                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "other@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Null(resent);
        Assert.Equal(upsertsBefore, accountStore.UpsertCount);
        Assert.Equal(Hash(first), accounts.GetByUsername("newbie")!.ActivationTokenHash);
    }

    [Fact]
    public void ResendVerification_MissingAccount_IsIgnoredWithoutPublishingAnEvent()
    {
        var (accounts, bus, persistence, _, _) = Create();
        var accountStore = persistence.Store<AccountEntity>();
        var upsertsBefore = accountStore.UpsertCount;
        AccountRegistrationRequestedEvent? resent = null;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
            {
                resent = message;

                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("newbie", "new@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Null(resent);
        Assert.Equal(upsertsBefore, accountStore.UpsertCount);
        Assert.Null(accounts.GetByUsername("newbie"));
    }

    [Fact]
    public void ResendVerification_PrivilegedAccount_IsIgnoredEvenWithPendingMarker()
    {
        var (accounts, bus, persistence, _, _) = Create();
        accounts.Create("staff", "secret99", "staff@bie.test", AccountLevelType.Administrator);
        accounts.SetActive("staff", false);
        var account = accounts.GetByUsername("staff")!;
        account.IsPublicRegistrationPending = true;
        account.ActivationTokenHash = Hash("staff-token");
        account.ActivationTokenExpiresAtUtc = Now.AddHours(1);
        var originalHash = account.ActivationTokenHash;
        var accountStore = persistence.Store<AccountEntity>();
        var upsertsBefore = accountStore.UpsertCount;
        var eventCount = 0;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (_, _) =>
            {
                eventCount++;

                return Task.CompletedTask;
            }
        );

        var result = accounts.ResendVerification("staff", "staff@bie.test");

        Assert.Equal(AccountResendResultType.Ignored, result);
        Assert.Equal(0, eventCount);
        Assert.Equal(upsertsBefore, accountStore.UpsertCount);
        Assert.Equal(originalHash, account.ActivationTokenHash);
        Assert.False(account.IsActive);
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
    public void ResendVerification_RotatesTokenAndPublishesOneEvent()
    {
        var (accounts, bus, persistence, _, _) = Create();
        var first = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        var accountStore = persistence.Store<AccountEntity>();
        var upsertsBefore = accountStore.UpsertCount;
        AccountRegistrationRequestedEvent? resent = null;
        var eventCount = 0;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (message, _) =>
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
        Assert.Equal(upsertsBefore + 1, accountStore.UpsertCount);
        Assert.NotEqual(first, resent!.Token);
        Assert.Equal(Hash(resent.Token), accounts.GetByUsername("newbie")!.ActivationTokenHash);
        Assert.True(accounts.GetByUsername("newbie")!.IsPublicRegistrationPending);
    }

    [Fact]
    public void SetActive_BlocksPendingRegistrationAndClearsVerificationState()
    {
        var (accounts, _, _, _, _) = Create();
        var token = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;

        Assert.True(accounts.SetActive("newbie", false));

        var account = accounts.GetByUsername("newbie")!;
        Assert.False(account.IsPublicRegistrationPending);
        Assert.Empty(account.ActivationTokenHash);
        Assert.Null(account.ActivationTokenExpiresAtUtc);
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail(token));
        Assert.Equal(AccountResendResultType.Ignored, accounts.ResendVerification("newbie", "new@bie.test"));
    }

    [Fact]
    public void SetLevel_PromotingPendingRegistrationClearsVerificationState()
    {
        var (accounts, _, _, _, _) = Create();
        var token = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;

        Assert.True(accounts.SetLevel("newbie", AccountLevelType.Administrator));

        var account = accounts.GetByUsername("newbie")!;
        Assert.False(account.IsPublicRegistrationPending);
        Assert.Empty(account.ActivationTokenHash);
        Assert.Null(account.ActivationTokenExpiresAtUtc);
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail(token));
    }

    [Fact]
    public async Task VerifyAndResend_ConcurrentRequests_LeaveOneConsistentTransition()
    {
        var (accounts, bus, _, _, _) = Create();
        var token = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        var resendEvents = 0;
        bus.Subscribe<AccountRegistrationRequestedEvent>(
            (_, _) =>
            {
                Interlocked.Increment(ref resendEvents);

                return Task.CompletedTask;
            }
        );

        var results = await RunConcurrently(
                          () => $"verify:{accounts.VerifyEmail(token)}",
                          () => $"resend:{accounts.ResendVerification("newbie", "new@bie.test")}"
                      );
        var account = accounts.GetByUsername("newbie")!;

        var verifiedFirst = results.Contains("verify:Verified") && results.Contains("resend:Ignored");
        var resentFirst = results.Contains("resend:Sent") && results.Contains("verify:InvalidToken");
        Assert.True(verifiedFirst || resentFirst);

        if (verifiedFirst)
        {
            Assert.True(account.IsActive);
            Assert.False(account.IsPublicRegistrationPending);
            Assert.Equal(0, resendEvents);
        }
        else
        {
            Assert.False(account.IsActive);
            Assert.True(account.IsPublicRegistrationPending);
            Assert.NotEmpty(account.ActivationTokenHash);
            Assert.Equal(1, resendEvents);
        }
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
        Assert.True(account.IsPublicRegistrationPending);
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
        Assert.False(accounts.GetByUsername("newbie")!.IsPublicRegistrationPending);

        // consumed token no longer matches
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail(token));
    }

    [Fact]
    public void VerifyEmail_BlockedPlayerAndPrivilegedAccount_CannotBeReactivated()
    {
        var (accounts, _, _, _, _) = Create();
        accounts.Create("blocked", "secret99", "blocked@bie.test", AccountLevelType.Player);
        accounts.SetActive("blocked", false);
        var blocked = accounts.GetByUsername("blocked")!;
        blocked.ActivationTokenHash = Hash("blocked-token");
        blocked.ActivationTokenExpiresAtUtc = Now.AddHours(1);

        accounts.Create("staff", "secret99", "staff@bie.test", AccountLevelType.Administrator);
        accounts.SetActive("staff", false);
        var staff = accounts.GetByUsername("staff")!;
        staff.IsPublicRegistrationPending = true;
        staff.ActivationTokenHash = Hash("staff-token");
        staff.ActivationTokenExpiresAtUtc = Now.AddHours(1);

        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("blocked-token"));
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("staff-token"));
        Assert.False(blocked.IsActive);
        Assert.False(staff.IsActive);
        Assert.Equal(Hash("blocked-token"), blocked.ActivationTokenHash);
        Assert.Equal(Hash("staff-token"), staff.ActivationTokenHash);
    }

    [Fact]
    public async Task VerifyEmail_ConcurrentRequests_ConsumeTokenExactlyOnce()
    {
        var (accounts, _, _, _, _) = Create();
        var token = accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        var actions = Enumerable
                      .Range(0, 16)
                      .Select(_ => (Func<AccountVerifyResultType>)(() => accounts.VerifyEmail(token)))
                      .ToArray();

        var results = await RunConcurrently(actions);

        Assert.Equal(1, results.Count(result => result == AccountVerifyResultType.Verified));
        Assert.Equal(15, results.Count(result => result == AccountVerifyResultType.InvalidToken));
        Assert.True(accounts.GetByUsername("newbie")!.IsActive);
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
        Assert.True(accounts.GetByUsername("newbie")!.IsPublicRegistrationPending);
    }

    [Fact]
    public void VerifyEmail_UnknownOrBlankToken_IsInvalid()
    {
        var (accounts, _, _, _, _) = Create();

        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("nope"));
        Assert.Equal(AccountVerifyResultType.InvalidToken, accounts.VerifyEmail("  "));
    }

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
                   new(persistence, characters, sessions, bus, timeProvider ?? new FixedTimeProvider(Now)),
                   bus,
                   persistence,
                   characters,
                   sessions
               );
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task<TResult[]> RunConcurrently<TResult>(params Func<TResult>[] actions)
    {
        using var start = new Barrier(actions.Length + 1);
        var tasks = actions
                    .Select(
                        action =>
                            Task.Factory.StartNew(
                                () =>
                                {
                                    start.SignalAndWait();

                                    return action();
                                },
                                CancellationToken.None,
                                TaskCreationOptions.LongRunning,
                                TaskScheduler.Default
                            )
                    )
                    .ToArray();

        start.SignalAndWait();

        return await Task.WhenAll(tasks);
    }
}
