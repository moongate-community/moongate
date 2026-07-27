using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Players;
using Moongate.Http.Plugin.Endpoints.Players;
using Moongate.Http.Plugin.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Types;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Client;

namespace Moongate.Tests.Http.Endpoints.Players;

public class OnlinePlayerAdminEndpointsTests
{
    private const string Route = "/api/v1/admin/players/online";

    [Fact]
    public async Task List_WithoutToken_IsUnauthorized()
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

    [Theory, InlineData(AccountLevelType.Administrator), InlineData(AccountLevelType.GrandMaster)]
    public async Task List_AsStaff_WithNoPlayersInWorld_ReturnsAnEmptyArray(AccountLevelType level)
    {
        await using var server = await StartAsync(level);
        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync(Route);
        var players = await response.Content.ReadFromJsonAsync<OnlinePlayerMapResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(players!);
    }

    [Fact]
    public async Task List_ReturnsOnlyInWorldPlayersWithMapFields()
    {
        await using var server = await StartAsync();
        server.Sessions.Connections.Add(
            Session(
                "alice",
                new(0x11),
                new()
                {
                    Id = new(0x42),
                    Name = "Freydis",
                    MapId = (int)MapType.Trammel,
                    Position = new(1420, 1698, 10),
                    Direction = DirectionType.SouthEast | DirectionType.Running,
                    Body = 401,
                    SkinHue = new Hue(1002),
                    Hits = 48,
                    HitsMax = 70,
                    Warmode = true
                },
                SessionStateType.InWorld
            )
        );
        server.Sessions.Connections.Add(
            Session(
                "waiting",
                new(0x12),
                new() { Id = new(0x43), Name = "Not Yet In World" },
                SessionStateType.Authenticated
            )
        );
        server.Sessions.Connections.Add(Session("broken", new(0x13), null, SessionStateType.InWorld));
        await server.AuthenticateAsync();

        var players = await server.Client.GetFromJsonAsync<OnlinePlayerMapResponse[]>(Route);
        var player = Assert.Single(players!);

        Assert.Equal("0x00000042", player.CharacterSerial);
        Assert.Equal("Freydis", player.CharacterName);
        Assert.Equal("0x00000011", player.AccountSerial);
        Assert.Equal("alice", player.AccountUsername);
        Assert.Equal(1, player.MapId);
        Assert.Equal("Trammel", player.MapName);
        Assert.Equal(1420, player.X);
        Assert.Equal(1698, player.Y);
        Assert.Equal(10, player.Z);
        Assert.Equal("SouthEast", player.Direction);
        Assert.True(player.Running);
        Assert.Equal(401, player.Body);
        Assert.Equal(1002, player.SkinHue);
        Assert.Equal(48, player.Hits);
        Assert.Equal(70, player.HitsMax);
        Assert.True(player.Warmode);
    }

    [Fact]
    public async Task List_UnknownMapId_UsesUnknownMapName()
    {
        await using var server = await StartAsync();
        server.Sessions.Connections.Add(
            Session(
                "alice",
                new(0x11),
                new() { Id = new(0x42), Name = "Freydis", MapId = 99 },
                SessionStateType.InWorld
            )
        );
        await server.AuthenticateAsync();

        var players = await server.Client.GetFromJsonAsync<OnlinePlayerMapResponse[]>(Route);

        Assert.Equal("Unknown", Assert.Single(players!).MapName);
    }

    [Fact]
    public async Task List_OrdersByCharacterNameThenSerial()
    {
        await using var server = await StartAsync();
        server.Sessions.Connections.Add(
            Session("one", new(1), new() { Id = new(3), Name = "Cedric" }, SessionStateType.InWorld)
        );
        server.Sessions.Connections.Add(
            Session("two", new(2), new() { Id = new(2), Name = "aramis" }, SessionStateType.InWorld)
        );
        server.Sessions.Connections.Add(
            Session("three", new(3), new() { Id = new(1), Name = "Aramis" }, SessionStateType.InWorld)
        );
        await server.AuthenticateAsync();

        var players = await server.Client.GetFromJsonAsync<OnlinePlayerMapResponse[]>(Route);

        Assert.Equal(
            ["0x00000001", "0x00000002", "0x00000003"],
            players!.Select(player => player.CharacterSerial)
        );
    }

    private static PlayerSession Session(
        string username,
        Serial accountId,
        MobileEntity? character,
        SessionStateType state
    )
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new SquidStdTcpClient(socket, Stream.Null);
        var session = new PlayerSession(client);

        session.SetAccountId(accountId);
        session.MarkAuthenticated(username);

        if (character is not null)
        {
            session.SetCharacter(character);
        }

        session.SetState(state);

        return session;
    }

    private static Task<TestApiServer> StartAsync(
        AccountLevelType level = AccountLevelType.Administrator
    )
        => TestApiServer.StartAsync(
            level,
            configure: container => container.RegisterApiEndpoint<OnlinePlayerAdminEndpoints>()
        );
}
