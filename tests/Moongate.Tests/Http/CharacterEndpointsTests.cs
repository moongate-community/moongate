using System.Net;
using System.Net.Http.Json;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Server.Data.Api;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http;

public class CharacterEndpointsTests
{
    private const string Route = "/api/v1/player/me/characters";

    [Fact]
    public async Task GetMyCharacters_WithoutAToken_IsUnauthorized()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task GetMyCharacters_NoCharacters_IsAnEmptyList()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var characters = await server.Client.GetFromJsonAsync<List<CharacterResponse>>(Route);

        Assert.NotNull(characters);
        Assert.Empty(characters);
    }

    [Fact]
    public async Task GetMyCharacters_ReturnsTheCallersCharacters()
    {
        await using var server = await TestApiServer.StartAsync();
        var mine = server.Characters.CreateCharacter(server.Accounts.GetByUsername("tom")!.Id, Packet("Freydis"));
        await server.AuthenticateAsync();

        var characters = await server.Client.GetFromJsonAsync<List<CharacterResponse>>(Route);

        var only = Assert.Single(characters!);
        Assert.Equal(mine.Id.ToString(), only.Serial);
        Assert.Equal("Freydis", only.Name);

        // The caller already knows who they are; naming the owner back at them is noise.
        Assert.Null(only.AccountUsername);
    }

    [Fact]
    public async Task GetMyCharacters_NeverReturnsAnotherAccountsCharacters()
    {
        // The one defect here that would be a security bug rather than an inconvenience.
        await using var server = await TestApiServer.StartAsync();
        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);
        server.Characters.CreateCharacter(server.Accounts.GetByUsername("alice")!.Id, Packet("Aramis"));
        await server.AuthenticateAsync();

        var characters = await server.Client.GetFromJsonAsync<List<CharacterResponse>>(Route);

        Assert.Empty(characters!);
    }

    [Fact]
    public async Task GetMyCharacters_IgnoresMobilesOwnedByNobody()
    {
        // Every NPC on a real shard looks like this in the store: a mobile no account lists.
        await using var server = await TestApiServer.StartAsync();
        await server.Persistence.Store<Moongate.Persistence.Entities.MobileEntity>()
                    .UpsertAsync(new() { Name = "a wandering healer" });
        await server.AuthenticateAsync();

        var characters = await server.Client.GetFromJsonAsync<List<CharacterResponse>>(Route);

        Assert.Empty(characters!);
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
