using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Endpoints.Accounts;

public class AccountEndpointsTests
{
    [Fact]
    public async Task List_ReportsEveryAccount()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync("/api/v1/admin/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tom", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_KnownAccount_ReportsIt()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync("/api/v1/admin/accounts/tom");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Administrator", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UnknownAccount_Is404()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync("/api/v1/admin/accounts/nobody")).StatusCode
        );
    }

    [Fact]
    public async Task Responses_NeverCarryTheSecrets()
    {
        // Against the raw JSON, not a deserialized DTO: deserializing into AccountResponse would drop an
        // extra field silently, which is exactly the leak this guards. AccountEntity holds both.
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        foreach (var route in new[] { "/api/v1/admin/accounts", "/api/v1/admin/accounts/tom" })
        {
            var body = await server.Client.GetStringAsync(route);

            Assert.DoesNotContain("assword", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ctivationToken", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task List_WithoutAToken_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await server.Client.GetAsync("/api/v1/admin/accounts")).StatusCode
        );
    }

    [Fact]
    public async Task List_WithPlayerToken_Is403()
    {
        await using var server = await TestApiServer.StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await server.Client.GetAsync("/api/v1/admin/accounts")).StatusCode
        );
    }

    [Fact]
    public async Task Create_NewAccount_Is201WithLocation()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "alice", password = "secret", email = "a@b.c", level = "Player" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/admin/accounts/alice", response.Headers.Location?.ToString());
        Assert.NotNull(server.Accounts.GetByUsername("alice"));
    }

    [Fact]
    public async Task Create_OmittedLevel_DefaultsToPlayer()
    {
        // The safe default: an account that gains staff rights by accident is the wrong way to fail.
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "alice", password = "secret" }
        );

        Assert.Equal(AccountLevelType.Player, server.Accounts.GetByUsername("alice")!.AccountLevel);
    }

    [Fact]
    public async Task Create_TakenUsername_Is409()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "tom", password = "secret" }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("alice", "")]
    public async Task Create_EmptyUsernameOrPassword_Is400(string username, string password)
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username, password }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownLevel_Is400AndCreatesNothing()
    {
        // The level is checked before the write, so a bad request cannot half-apply.
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "alice", password = "secret", level = "Wizard" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(server.Accounts.GetByUsername("alice"));
    }

    [Fact]
    public async Task Update_ChangesOnlyTheFieldsSent()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PatchAsJsonAsync(
            "/api/v1/admin/accounts/tom",
            new { isActive = false }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var account = server.Accounts.GetByUsername("tom")!;
        Assert.False(account.IsActive);
        // Untouched: the level was not in the body, which is what makes this a PATCH.
        Assert.Equal(AccountLevelType.Administrator, account.AccountLevel);
    }

    [Fact]
    public async Task Update_ChangesTheLevel()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        await server.Client.PatchAsJsonAsync("/api/v1/admin/accounts/tom", new { level = "GrandMaster" });

        Assert.Equal(AccountLevelType.GrandMaster, server.Accounts.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public async Task Update_ChangesThePassword()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        await server.Client.PatchAsJsonAsync("/api/v1/admin/accounts/tom", new { password = "nuova" });

        // Proved through the service rather than by reading a hash back: the new password must
        // authenticate and the old one must not.
        Assert.True(server.Accounts.Authenticate("tom", "nuova").Success);
        Assert.False(server.Accounts.Authenticate("tom", "secret").Success);
    }

    [Fact]
    public async Task Update_UnknownAccount_Is404()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PatchAsJsonAsync(
            "/api/v1/admin/accounts/nobody",
            new { isActive = false }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownLevel_Is400AndChangesNothing()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PatchAsJsonAsync(
            "/api/v1/admin/accounts/tom",
            new { level = "Wizard", isActive = false }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Nothing applied: the level is validated before any setter runs.
        var account = server.Accounts.GetByUsername("tom")!;
        Assert.True(account.IsActive);
        Assert.Equal(AccountLevelType.Administrator, account.AccountLevel);
    }

    [Fact]
    public async Task Delete_KnownAccount_Is204AndRemovesIt()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.DeleteAsync("/api/v1/admin/accounts/tom");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(server.Accounts.GetByUsername("tom"));
    }

    [Fact]
    public async Task Delete_RunsOnTheGameLoop()
    {
        // Delete checks IsCharacterPlayed and then deletes, and login runs on the loop. Doing the work
        // anywhere else lets a player log in between the two and lose their character. This pins the
        // handover, which no status code would reveal.
        var loop = new StubGameLoopContext();
        await using var server = await TestApiServer.StartAsync(loop: loop);
        await server.AuthenticateAsync();

        await server.Client.DeleteAsync("/api/v1/admin/accounts/tom");

        Assert.Equal(1, loop.PostCount);
    }

    [Fact]
    public async Task Delete_CharacterBeingPlayed_Is409AndKeepsTheAccount()
    {
        // Deleting an account whose character is logged in would pull the world out from under them.
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var account = server.Accounts.GetByUsername("tom")!;
        var mobile = server.Characters.CreateCharacter(account.Id, CharacterPacket());
        server.Sessions.Played.Add(mobile!.Id);

        var response = await server.Client.DeleteAsync("/api/v1/admin/accounts/tom");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(server.Accounts.GetByUsername("tom"));
    }

    private static CharacterCreationPacket CharacterPacket()
        => new(
            0,
            "Freydis",
            0,
            4,
            GenderType.Female,
            RaceType.Elf,
            45,
            20,
            25,
            [new(1, 50)],
            0x03EA,
            0x203C,
            0x044E,
            0x2040,
            0x0450,
            0,
            0x0765,
            0x0766
        );

    [Fact]
    public async Task Delete_UnknownAccount_Is404()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.DeleteAsync("/api/v1/admin/accounts/nobody")).StatusCode
        );
    }

    [Fact]
    public async Task Delete_LoopNeverAnswers_Is503()
    {
        await using var server = await TestApiServer.StartAsync(
            loop: new StubGameLoopContext(answers: false),
            deleteTimeout: TimeSpan.FromMilliseconds(50)
        );
        await server.AuthenticateAsync();

        var response = await server.Client.DeleteAsync("/api/v1/admin/accounts/tom");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // Still there: a timeout must not read as a delete.
        Assert.NotNull(server.Accounts.GetByUsername("tom"));
    }
}
