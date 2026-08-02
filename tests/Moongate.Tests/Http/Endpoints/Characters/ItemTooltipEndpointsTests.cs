using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Http.Plugin.Services.Characters;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Localization;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Endpoints.Characters;

/// <summary>
/// The route is nested under the character on purpose: an item knows its container, not whose it is,
/// so authorization comes from the character and the item must actually be in that character's tree.
/// That pairing is what these tests are about.
/// </summary>
public class ItemTooltipEndpointsTests
{
    private const int WeightCliloc = 1072788;

    [Fact]
    public async Task Get_AnItemInTheBackpack_ReturnsItsRenderedLines()
    {
        var world = new StubItemService([]);
        await using var server = await StartAsync(world, AccountLevelType.Player);
        var character = Create(server, "tom", "Freydis");
        var dagger = Backpack(server, world, character, "a dagger");

        await server.AuthenticateAsync();

        var tooltip = await server.Client.GetFromJsonAsync<ItemTooltipResponse>(Route(character, dagger));

        Assert.Equal("Weight: 3 stone", Assert.Single(tooltip!.Lines));
        Assert.Equal("dagger", tooltip.TemplateId);
    }

    // Equipment is as much the character's as the backpack is, and a worn item is exactly what someone
    // hovers a paperdoll layer to ask about.
    [Fact]
    public async Task Get_AWornItem_ReturnsItsLines()
    {
        var world = new StubItemService([]);
        await using var server = await StartAsync(world, AccountLevelType.Player);
        var character = Create(server, "tom", "Freydis");
        var robe = Worn(server, world, character, "a robe");

        await server.AuthenticateAsync();

        var tooltip = await server.Client.GetFromJsonAsync<ItemTooltipResponse>(Route(character, robe));

        Assert.Single(tooltip!.Lines);
    }

    // The check that makes nesting worth doing: an item that exists, but not on this character.
    [Fact]
    public async Task Get_AnItemNotInThatCharactersTree_IsNotFound()
    {
        var world = new StubItemService([]);
        await using var server = await StartAsync(world, AccountLevelType.Player);
        var character = Create(server, "tom", "Freydis");

        Backpack(server, world, character, "a dagger");

        // A real item, owned by nobody in this character's tree.
        var loose = world.Track(Item(0x40009999, "a loose sword"));

        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync(Route(character, loose.Id.ToString()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_AnotherAccountsCharacter_AsAPlayer_IsForbidden()
    {
        var world = new StubItemService([]);
        await using var server = await StartAsync(world, AccountLevelType.Player);

        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);

        var hers = Create(server, "alice", "Aramis");
        var dagger = Backpack(server, world, hers, "a dagger");

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync(Route(hers, dagger))).StatusCode);
    }

    [Fact]
    public async Task Get_AnotherAccountsCharacter_AsStaff_ReturnsIt()
    {
        var world = new StubItemService([]);
        await using var server = await StartAsync(world, AccountLevelType.Administrator);

        server.Accounts.Create("alice", "secret", null, AccountLevelType.Player);

        var hers = Create(server, "alice", "Aramis");
        var dagger = Backpack(server, world, hers, "a dagger");

        await server.AuthenticateAsync();

        var tooltip = await server.Client.GetFromJsonAsync<ItemTooltipResponse>(Route(hers, dagger));

        Assert.Equal(dagger, tooltip!.Serial);
    }

    [Fact]
    public async Task Get_AnUnknownCharacter_IsNotFound()
    {
        await using var server = await StartAsync(new StubItemService([]));

        await server.AuthenticateAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync(Route("0x0000DEAD", "0x40000001"))).StatusCode
        );
    }

    [Fact]
    public async Task Get_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync(new StubItemService([]));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await server.Client.GetAsync(Route("0x00000001", "0x40000001"))).StatusCode
        );
    }

    // A shard with no client files describes no cliloc. The technical fields are still worth showing,
    // so an empty list beats a 500.
    [Fact]
    public async Task Get_WithNoStringTable_ReturnsNoLinesRatherThanFailing()
    {
        var world = new StubItemService([]);
        await using var server = await StartAsync(world, AccountLevelType.Player, clilocs: new StubClilocService());
        var character = Create(server, "tom", "Freydis");
        var dagger = Backpack(server, world, character, "a dagger");

        await server.AuthenticateAsync();

        var tooltip = await server.Client.GetFromJsonAsync<ItemTooltipResponse>(Route(character, dagger));

        Assert.Empty(tooltip!.Lines);
        Assert.Equal("dagger", tooltip.TemplateId);
    }

    private static string Route(string character, string item)
        => $"/api/v1/characters/{character}/items/{item}/tooltip";

    /// <summary>Puts an item in the character's backpack, creating the backpack if it has none.</summary>
    private static string Backpack(TestApiServer server, StubItemService world, string character, string name)
    {
        var mobile = Mobile(server, character);

        // Character creation already sets a BackpackId, but it names an item this stub world does not
        // hold — so the test gives the mobile a backpack it can actually resolve.
        if (world.GetById(mobile.BackpackId) is null)
        {
            var pack = world.Track(Item(0x40000100, "a backpack"));

            mobile.BackpackId = pack.Id;
            mobile.EquippedItemIds[LayerType.Backpack] = pack.Id;
        }

        var item = world.Track(Item((uint)(0x40000200 + world.TrackedCount), name));

        world.GetById(mobile.BackpackId)!.ContainedItemIds.Add(item.Id);

        return item.Id.ToString();
    }

    private static string Worn(TestApiServer server, StubItemService world, string character, string name)
    {
        var mobile = Mobile(server, character);
        var item = world.Track(Item((uint)(0x40000300 + world.TrackedCount), name));

        item.EquippedLayer = LayerType.OuterTorso;
        mobile.EquippedItemIds[LayerType.OuterTorso] = item.Id;
        world.Equipped.Add(item);

        return item.Id.ToString();
    }

    private static MobileEntity Mobile(TestApiServer server, string character)
    {
        Serial.TryParse(character, out var serial);

        return server.Persistence.Store<MobileEntity>().GetById(serial)!;
    }

    private static ItemEntity Item(uint serial, string name)
        => new()
        {
            Id = new(serial),
            Name = name,
            TemplateId = "dagger",
            ItemId = 0x13B9,
            Amount = 1,
        };

    private static string Create(TestApiServer server, string username, string name)
    {
        var accountId = server.Accounts.GetByUsername(username)!.Id;

        server.Characters.CreateCharacter(accountId, Packet(name));

        return server.Accounts.GetByUsername(username)!.MobileIds[^1].ToString();
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

    private static async Task<TestApiServer> StartAsync(
        StubItemService world,
        AccountLevelType level = AccountLevelType.Administrator,
        IClilocService? clilocs = null
    )
        => await TestApiServer.StartAsync(
               level,
               configure: container =>
                          {
                              container.RegisterInstance<IItemService>(world);
                              container.RegisterInstance(
                                  clilocs ?? StubClilocService.Entries((WeightCliloc, "Weight: ~1_WEIGHT~ stone"))
                              );
                              container.RegisterInstance<IOplService>(new StubOplService(WeightCliloc));
                              container.Register<ICharacterQueryService, CharacterQueryService>(Reuse.Singleton);
                              container.Register<CharacterAccessService>(Reuse.Singleton);
                              container.Register<CharacterInventoryReader>(Reuse.Singleton);
                              container.Register<OplTextRenderer>(Reuse.Singleton);
                              container.RegisterApiEndpointInstance(
                                  new ItemTooltipEndpoints(
                                      container.Resolve<CharacterAccessService>(),
                                      container.Resolve<CharacterInventoryReader>(),
                                      container.Resolve<IItemService>(),
                                      container.Resolve<IOplService>(),
                                      container.Resolve<OplTextRenderer>(),
                                      container.Resolve<IGameLoopContext>()
                                  )
                              );
                          }
           );
}
