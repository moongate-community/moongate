using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Http.Plugin.Services.Characters;
using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Skills;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Endpoints.Characters;

/// <summary>
/// One route serving two audiences, so what it owns is the decision of who may read what: your own
/// always, anyone's for staff, and nothing at all for a character on someone else's account.
/// </summary>
public class CharacterDetailEndpointsTests
{
    [Fact]
    public async Task Get_TheCallersOwnCharacter_ReturnsItWithEquipmentAndBackpack()
    {
        await using var server = await StartAsync(AccountLevelType.Player);
        var character = Create(server, "tom", "Freydis");

        await server.AuthenticateAsync();

        var detail = await server.Client.GetFromJsonAsync<CharacterDetailResponse>($"/api/v1/characters/{character}");

        Assert.Equal("Freydis", detail!.Character.Name);
        Assert.NotNull(detail.Equipment);
        Assert.NotNull(detail.Backpack);
    }

    // The entity stores 500 tenths; the API reports 50 points. A consumer that forgot the division
    // would publish a character with 500.0 alchemy, so the route is asserted in the reported unit.
    [Fact]
    public async Task Get_ReportsTheCharactersSkillsInPoints()
    {
        await using var server = await StartAsync(AccountLevelType.Player);
        var character = Create(server, "tom", "Freydis");

        await server.AuthenticateAsync();

        var detail = await server.Client.GetFromJsonAsync<CharacterDetailResponse>($"/api/v1/characters/{character}");

        var skill = Assert.Single(detail!.Skills);

        Assert.Equal("Alchemy", skill.Name);
        Assert.Equal(50.0, skill.Value);
        Assert.Equal("Up", skill.Lock);
    }

    // The account comes from the token. A player who could read another account's character by
    // changing a number in the URL is the whole reason this check exists.
    [Fact]
    public async Task Get_AnotherAccountsCharacter_AsAPlayer_IsForbidden()
    {
        await using var server = await StartAsync(AccountLevelType.Player);

        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);

        var hers = Create(server, "alice", "Aramis");

        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync($"/api/v1/characters/{hers}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AnotherAccountsCharacter_AsStaff_ReturnsIt()
    {
        await using var server = await StartAsync(AccountLevelType.Administrator);

        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);

        var hers = Create(server, "alice", "Aramis");

        await server.AuthenticateAsync();

        var detail = await server.Client.GetFromJsonAsync<CharacterDetailResponse>($"/api/v1/characters/{hers}");

        Assert.Equal("Aramis", detail!.Character.Name);
        Assert.Equal("alice", detail.Character.AccountUsername);
    }

    [Fact]
    public async Task Get_AnUnknownSerial_IsNotFound()
    {
        await using var server = await StartAsync();

        await server.AuthenticateAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync("/api/v1/characters/0x0000DEAD")).StatusCode
        );
    }

    // The API reports serials as 0x40000001, so its own value must address its own route -- the exact
    // defect fixed on the image routes in PR #167.
    [Fact]
    public async Task Get_AcceptsTheHexSerialTheApiReports()
    {
        await using var server = await StartAsync(AccountLevelType.Player);
        var character = Create(server, "tom", "Freydis");

        await server.AuthenticateAsync();

        // Create returns the serial already in the API's own form; asserting that keeps this test
        // honest if that form ever changes.
        Assert.StartsWith("0x", character);

        var response = await server.Client.GetAsync($"/api/v1/characters/{character}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithASerialThatIsNotANumber_IsNotFound()
    {
        await using var server = await StartAsync();

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync("/api/v1/characters/banana")).StatusCode);
    }

    [Fact]
    public async Task Get_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await server.Client.GetAsync("/api/v1/characters/0x00000001")).StatusCode
        );
    }

    private static string Create(TestApiServer server, string username, string name)
    {
        var accountId = server.Accounts.GetByUsername(username)!.Id;

        server.Characters.CreateCharacter(accountId, Packet(name));

        return server.Accounts.GetByUsername(username)!.MobileIds[^1].ToString();
    }

    /// <summary>The two skills the creation packet below hands out, so the route can name them.</summary>
    private static ISkillService SkillRegistry()
    {
        var skills = new SkillService();

        skills.Register(new() { Id = 1, Name = "Alchemy" });

        return skills;
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

    private static async Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
        => await TestApiServer.StartAsync(
               level,
               configure: container =>
                          {
                              // What this route owns is who may read what; the walk over equipment and
                              // containers has its own tests, so an empty item world is enough here.
                              container.RegisterInstance<IItemService>(new StubItemService([]));
                              container.Register<ICharacterQueryService, CharacterQueryService>(Reuse.Singleton);
                              container.Register<CharacterInventoryReader>(Reuse.Singleton);
                              container.RegisterInstance<ISkillService>(SkillRegistry());
                              container.Register<CharacterSkillReader>(Reuse.Singleton);
                              container.RegisterApiEndpointInstance(
                                  new CharacterDetailEndpoints(
                                      container.Resolve<IAccountService>(),
                                      container.Resolve<ICharacterQueryService>(),
                                      container.Resolve<CharacterInventoryReader>(),
                                      container.Resolve<CharacterSkillReader>()
                                  )
                              );
                          }
           );
}
