using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Registration;
using Moongate.Server.Abstractions.Data;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.Registration;

public sealed class RegistrationEndpointsTests
{
    [Fact]
    public async Task Register_WhenDisabled_Is403()
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(registrationRateLimiter: limiter);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret99", "new@bie.test")
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(limiter.Keys);
    }

    [Fact]
    public async Task Register_WhenEnabled_CreatesInactive_202()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate
                { RegistrationEnabled = true, Contacts = new() { Website = "https://shard.example" } }
        );

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret99", "new@bie.test")
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.False(server.Accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public async Task Register_WhenRuntimeEmailIsUnavailable_Is503()
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(
            emailChannelReady: false,
            registrationRateLimiter: limiter
        );
        EnableRegistration(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret99", "new@bie.test")
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(limiter.Keys);
    }

    [Fact]
    public async Task Register_InvalidUsername_IsValidationProblem400()
    {
        await using var server = await TestApiServer.StartAsync();
        EnableRegistration(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("ab", "secret99", "new@bie.test")
        );

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("username", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_InvalidPassword_IsValidationProblem400()
    {
        await using var server = await TestApiServer.StartAsync();
        EnableRegistration(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "short", "new@bie.test")
        );

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("password", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_DuplicateUsernameOrEmail_IsGeneric409()
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(registrationRateLimiter: limiter);
        EnableRegistration(server);
        await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret99", "new@bie.test")
        );

        var duplicateUsername = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret99", "other@bie.test")
        );
        var duplicateEmail = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("othername", "secret99", "new@bie.test")
        );
        var usernameProblem = await duplicateUsername.Content.ReadFromJsonAsync<ProblemDetails>();
        var emailProblem = await duplicateEmail.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, duplicateUsername.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);
        Assert.Equal(usernameProblem!.Detail, emailProblem!.Detail);
        Assert.DoesNotContain("newbie", usernameProblem.Detail!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new@bie.test", usernameProblem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_ActivatesAccount()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate
                { RegistrationEnabled = true, Contacts = new() { Website = "https://shard.example" } }
        );
        var token = server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;

        var response = await server.Client.PostAsJsonAsync("/api/v1/register/verify", new VerifyEmailRequest(token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(server.Accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public async Task Verify_ExpiredToken_Is410()
    {
        var clock = MutableTimeProvider.StartingNow();
        await using var server = await TestApiServer.StartAsync(clock: clock);
        var token = server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;
        clock.Advance(TimeSpan.FromHours(24));

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/verify",
            new VerifyEmailRequest(token)
        );

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WhenRegistrationWasDisabledAfterCreation_StillActivates()
    {
        await using var server = await TestApiServer.StartAsync();
        var token = server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/verify",
            new VerifyEmailRequest(token)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(server.Accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public async Task VerifyAndResend_AdminBlockedPendingAccount_CannotReactivateOrRotate()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );
        var token = server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test").Token!;

        Assert.True(server.Accounts.SetActive("newbie", false));

        var verify = await server.Client.PostAsJsonAsync(
            "/api/v1/register/verify",
            new VerifyEmailRequest(token)
        );
        var resend = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "newbie", email = "new@bie.test" }
        );
        var account = server.Accounts.GetByUsername("newbie")!;

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.False(account.IsActive);
        Assert.False(account.IsPublicRegistrationPending);
        Assert.Empty(account.ActivationTokenHash);
        Assert.Null(account.ActivationTokenExpiresAtUtc);
    }

    [Fact]
    public async Task VerifyAndResend_PrivilegedAccount_CannotReactivateOrRotate()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );
        server.Accounts.Create("staff", "secret99", "staff@bie.test", AccountLevelType.Administrator);
        server.Accounts.SetActive("staff", false);
        var account = server.Accounts.GetByUsername("staff")!;
        account.IsPublicRegistrationPending = true;
        account.ActivationTokenHash = HashToken("staff-token");
        account.ActivationTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1);

        var verify = await server.Client.PostAsJsonAsync(
            "/api/v1/register/verify",
            new VerifyEmailRequest("staff-token")
        );
        var resend = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "staff", email = "staff@bie.test" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.False(account.IsActive);
        Assert.Equal(HashToken("staff-token"), account.ActivationTokenHash);
    }

    [Fact]
    public async Task Verify_BadToken_Is400()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.PostAsJsonAsync("/api/v1/register/verify", new VerifyEmailRequest("nope"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_RateLimited_429()
    {
        // The fixture seeds a limiter of 2/window; the 3rd call from the same loopback IP is throttled.
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate
                { RegistrationEnabled = true, Contacts = new() { Website = "https://shard.example" } }
        );

        await server.Client.PostAsJsonAsync("/api/v1/register", new RegisterRequest("first", "secret99", "a@b.test"));
        await server.Client.PostAsJsonAsync("/api/v1/register", new RegisterRequest("second", "secret99", "b@b.test"));
        var third = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("third", "secret99", "c@b.test")
        );

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task Resend_MatchingPendingAccount_RotatesToken_202()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );
        server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test");
        var originalHash = server.Accounts.GetByUsername("newbie")!.ActivationTokenHash;

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "newbie", email = "new@bie.test" }
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotEqual(originalHash, server.Accounts.GetByUsername("newbie")!.ActivationTokenHash);
    }

    [Theory]
    [InlineData("missing", "missing@bie.test")]
    [InlineData("tom", "tom@bie.test")]
    [InlineData("newbie", "other@bie.test")]
    public async Task Resend_MissingActiveOrMismatched_IsAlways202(string username, string email)
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(registrationRateLimiter: limiter);
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );
        server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test");

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username, email }
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", "new@bie.test", "username")]
    [InlineData("newbie", "not-an-email", "email")]
    public async Task Resend_InvalidInput_Is400(string username, string email, string expectedKey)
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(registrationRateLimiter: limiter);
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username, email }
        );
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedKey, problem!.Errors.Keys);
        Assert.Equal(2, limiter.Keys.Count);
        Assert.StartsWith("resend:ip:", limiter.Keys[0], StringComparison.Ordinal);
        Assert.StartsWith("resend:target:", limiter.Keys[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resend_WhenRegistrationDisabled_StillQueuesForPendingAccount()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );
        server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test");
        var originalHash = server.Accounts.GetByUsername("newbie")!.ActivationTokenHash;

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "newbie", email = "new@bie.test" }
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.False(server.ServerSettings.Get().RegistrationEnabled);
        Assert.NotEqual(originalHash, server.Accounts.GetByUsername("newbie")!.ActivationTokenHash);
    }

    [Fact]
    public async Task Resend_WhenEmailUnavailable_Is503()
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(
            emailChannelReady: false,
            registrationRateLimiter: limiter
        );
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "newbie", email = "new@bie.test" }
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(limiter.Keys);
    }

    [Fact]
    public async Task Resend_ThirdIpAttempt_Is429()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );

        await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "first", email = "first@bie.test" }
        );
        await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "second", email = "second@bie.test" }
        );
        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = "third", email = "third@bie.test" }
        );

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Resend_TargetBudgetIsIndependentFromIpBudget()
    {
        var limiter = new RecordingRegistrationRateLimiter(
            (key, attempt) => key.StartsWith("resend:ip:", StringComparison.Ordinal) || attempt <= 2
        );
        await using var server = await TestApiServer.StartAsync(registrationRateLimiter: limiter);
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );
        server.Accounts.RegisterPending("newbie", "secret99", "new@bie.test");

        var first = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = " newbie ", email = " new@bie.test " }
        );
        var second = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = " newbie ", email = " NEW@BIE.TEST " }
        );
        var third = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = " newbie ", email = " new@bie.test " }
        );
        var targetKeys = limiter.Keys.Where(key => key.StartsWith("resend:target:", StringComparison.Ordinal)).ToArray();

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(3, targetKeys.Length);
        Assert.Single(targetKeys.Distinct(StringComparer.Ordinal));
        Assert.DoesNotContain("newbie", targetKeys[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new@bie.test", targetKeys[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resend_NullInput_Is400()
    {
        var limiter = new RecordingRegistrationRateLimiter((_, _) => true);
        await using var server = await TestApiServer.StartAsync(registrationRateLimiter: limiter);
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Contacts = new() { Website = "https://shard.example" } }
        );

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register/resend",
            new { username = (string?)null, email = (string?)null }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, limiter.Keys.Count);
        Assert.StartsWith("resend:target:", limiter.Keys[1], StringComparison.Ordinal);
    }

    private static void EnableRegistration(TestApiServer server)
    {
        server.ServerSettings.Update(
            new ServerSettingsUpdate
                { RegistrationEnabled = true, Contacts = new() { Website = "https://shard.example" } }
        );
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
