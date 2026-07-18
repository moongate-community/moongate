using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Endpoints.Characters;

public class CharacterAdminEndpointsTests
{
    private const string Route = "/api/v1/admin/characters";

    private static async Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
        => await TestApiServer.StartAsync(
            level,
            configure: container =>
            {
                container.Register<ICharacterQueryService, CharacterQueryService>(Reuse.Singleton);
                container.RegisterApiEndpointInstance(
                    new CharacterAdminEndpoints(container.Resolve<ICharacterQueryService>())
                );
            }
        );

    [Fact]
    public async Task List_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task List_AsPlayer_IsForbidden()
    {
        await using var server = await StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task List_AsStaff_ReturnsEveryCharacterWithItsOwner()
    {
        await using var server = await StartAsync();
        server.Characters.CreateCharacter(server.Accounts.GetByUsername("tom")!.Id, Packet("Freydis"));
        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);
        server.Characters.CreateCharacter(server.Accounts.GetByUsername("alice")!.Id, Packet("Aramis"));

        // Every NPC on a real shard looks like this: a mobile no account lists. It is not a character.
        await server.Persistence.Store<MobileEntity>().UpsertAsync(new() { Name = "a wandering healer" });

        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<CharacterResponse>>(Route);

        Assert.Equal(2, page!.Total);
        Assert.All(page.Items, character => Assert.NotNull(character.AccountUsername));
        Assert.Contains(page.Items, character => character.AccountUsername == "alice");
    }

    [Fact]
    public async Task List_NoMatches_IsAnEmptyPageNotANotFound()
    {
        // The query ran and matched nothing. That is a fact, not a failure.
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync($"{Route}?search=nobody");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<CharacterResponse>>();

        Assert.Equal(0, page!.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task List_SearchMatchesTheCharacterName()
    {
        await using var server = await StartAsync();
        var tom = server.Accounts.GetByUsername("tom")!.Id;
        server.Characters.CreateCharacter(tom, Packet("Aramis"));
        server.Characters.CreateCharacter(tom, Packet("Bors"));
        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<CharacterResponse>>($"{Route}?search=ara");

        Assert.Equal("Aramis", Assert.Single(page!.Items).Name);
    }

    [Fact]
    public async Task List_SearchMatchesTheOwningUsername()
    {
        // The staff question is "find me so-and-so's character", so one box searches both.
        await using var server = await StartAsync();
        server.Characters.CreateCharacter(server.Accounts.GetByUsername("tom")!.Id, Packet("Aramis"));
        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);
        server.Characters.CreateCharacter(server.Accounts.GetByUsername("alice")!.Id, Packet("Bors"));
        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<CharacterResponse>>($"{Route}?search=alice");

        Assert.Equal("Bors", Assert.Single(page!.Items).Name);
    }

    [Fact]
    public async Task List_Pages()
    {
        await using var server = await StartAsync();
        var tom = server.Accounts.GetByUsername("tom")!.Id;
        server.Characters.CreateCharacter(tom, Packet("Aramis"));
        server.Characters.CreateCharacter(tom, Packet("Bors"));
        server.Characters.CreateCharacter(tom, Packet("Cedric"));
        await server.AuthenticateAsync();

        var first = await server.Client.GetFromJsonAsync<PagedResponse<CharacterResponse>>(
            $"{Route}?page=1&pageSize=2"
        );
        var second = await server.Client.GetFromJsonAsync<PagedResponse<CharacterResponse>>(
            $"{Route}?page=2&pageSize=2"
        );

        Assert.Equal(2, first!.Items.Count);
        Assert.Single(second!.Items);
        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.TotalPages);

        // Pages must not overlap, which is what the ordering key is for.
        Assert.Empty(first.Items.Select(c => c.Serial).Intersect(second.Items.Select(c => c.Serial)));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=5000")]
    public async Task List_OutOfRangePaging_IsBadRequest(string query)
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync($"{Route}{query}")).StatusCode);
    }

    private static CharacterCreationPacket Packet(string name)
        => new(
            0,
            name,
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
}
