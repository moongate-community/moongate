using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Contracts.V1;
using Moongate.Core.Geometry;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Tests.Integration.Admin;

/// <summary>
///     AdminPlayers and AdminOperations through the real gRPC host, the accounts database and the session store, with
///     real sessions and mobiles and fake save, backup and broadcast services.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class AdminWorldGrpcTests
{
    [Fact]
    public async Task ListOnlinePlayers_GivesTheCharactersInTheWorld_InOrder_AndPages()
    {
        await using var world = await World.CreateAsync();
        await world.PlayerAsync(30, 903, DomainAccountType.Regular, "Cara", MapType.Felucca, new(10, 20, 5));
        await world.PlayerAsync(10, 901, DomainAccountType.GameMaster, "Anna", MapType.Trammel, new(1, 2, 3));
        await world.PlayerAsync(20, 902, DomainAccountType.Administrator, "Bruno");
        await world.PlayerAsync(40, 904, DomainAccountType.Regular, "Ghost", entered: false);
        var headers = await world.LoginAsync(DomainAccountType.GameMaster);
        var client = new AdminPlayers.AdminPlayersClient(world.Fixture.Channel);

        var first = await client.ListOnlinePlayersAsync(new() { PageSize = 2 }, headers);
        var second = await client.ListOnlinePlayersAsync(new() { PageSize = 2, AfterCharacterId = first.NextAfterCharacterId }, headers);

        Assert.Equal(["Anna", "Bruno"], first.Players.Select(player => player.Name));
        Assert.Equal(20u, first.NextAfterCharacterId);
        Assert.Equal(["Cara"], second.Players.Select(player => player.Name));
        Assert.Equal(0u, second.NextAfterCharacterId);
        var anna = first.Players[0];
        Assert.Equal((901u, 10u, AccountType.GameMaster, (int)MapType.Trammel, 1, 2, 3), (anna.AccountId, anna.CharacterId, anna.AccountType, anna.Map, anna.X, anna.Y, anna.Z));
        Assert.True(anna.SessionId > 0);
    }

    [Fact]
    public async Task ListOnlinePlayers_PageSizeAboveTheLimit_IsInvalid()
    {
        await using var world = await World.CreateAsync();
        var headers = await world.LoginAsync(DomainAccountType.GameMaster);

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminPlayers.AdminPlayersClient(world.Fixture.Channel).ListOnlinePlayersAsync(new() { PageSize = 201 }, headers).ResponseAsync
        );

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Theory, InlineData(DomainAccountType.GameMaster, true), InlineData(DomainAccountType.Administrator, true), InlineData(DomainAccountType.Regular, false)]
    public async Task AdminPlayers_AreForGameMastersAndAdministrators(DomainAccountType role, bool allowed)
    {
        await using var world = await World.CreateAsync();
        await world.PlayerAsync(10, 901, DomainAccountType.Regular, "Anna");
        var headers = await world.LoginAsync(role);
        var client = new AdminPlayers.AdminPlayersClient(world.Fixture.Channel);

        if (allowed)
        {
            Assert.Single((await client.ListOnlinePlayersAsync(new(), headers)).Players);
            await client.KickPlayerAsync(new() { CharacterId = 10 }, headers);
            Assert.Equal([world.Sessions[10].SessionId], world.Game.Sender.Disconnected);

            return;
        }

        Assert.Equal(StatusCode.PermissionDenied, (await Assert.ThrowsAsync<RpcException>(() => client.ListOnlinePlayersAsync(new(), headers).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.PermissionDenied, (await Assert.ThrowsAsync<RpcException>(() => client.KickPlayerAsync(new() { CharacterId = 10 }, headers).ResponseAsync)).StatusCode);
        Assert.Empty(world.Game.Sender.Disconnected);
    }

    [Theory,
     InlineData(DomainAccountType.GameMaster, DomainAccountType.Regular, true),
     InlineData(DomainAccountType.GameMaster, DomainAccountType.GameMaster, false),
     InlineData(DomainAccountType.GameMaster, DomainAccountType.Administrator, false),
     InlineData(DomainAccountType.Administrator, DomainAccountType.Administrator, true),
     InlineData(DomainAccountType.Administrator, DomainAccountType.GameMaster, true)]
    public async Task KickPlayer_NeverAnAccountOfYourOwnLevelOrHigher_UnlessYouAreAnAdministrator(
        DomainAccountType caller,
        DomainAccountType target,
        bool allowed
    )
    {
        await using var world = await World.CreateAsync();
        await world.PlayerAsync(10, 901, target, "Target");
        var headers = await world.LoginAsync(caller);
        var client = new AdminPlayers.AdminPlayersClient(world.Fixture.Channel);

        if (allowed)
        {
            await client.KickPlayerAsync(new() { CharacterId = 10, Reason = "spam" }, headers);
            Assert.Equal([world.Sessions[10].SessionId], world.Game.Sender.Disconnected);

            return;
        }

        Assert.Equal(StatusCode.PermissionDenied, (await Assert.ThrowsAsync<RpcException>(() => client.KickPlayerAsync(new() { CharacterId = 10 }, headers).ResponseAsync)).StatusCode);
        Assert.Empty(world.Game.Sender.Disconnected);
    }

    [Fact]
    public async Task KickPlayer_YourOwnAccount_IsRefused()
    {
        await using var world = await World.CreateAsync();
        var headers = await world.LoginAsync(DomainAccountType.Administrator);
        await world.PlayerAsync(10, world.CallerAccountId, DomainAccountType.Administrator, "Me");

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminPlayers.AdminPlayersClient(world.Fixture.Channel).KickPlayerAsync(new() { CharacterId = 10 }, headers).ResponseAsync
        );

        Assert.Equal(StatusCode.FailedPrecondition, error.StatusCode);
        Assert.Empty(world.Game.Sender.Disconnected);
    }

    [Theory,
     InlineData(0u, "", StatusCode.InvalidArgument),
     InlineData(99u, "", StatusCode.NotFound),
     InlineData(10u, "x", StatusCode.OK)]
    public async Task KickPlayer_BadRequests_AreClassified(uint character, string reason, StatusCode expected)
    {
        await using var world = await World.CreateAsync();
        await world.PlayerAsync(10, 901, DomainAccountType.Regular, "Anna");
        var headers = await world.LoginAsync(DomainAccountType.Administrator);
        var client = new AdminPlayers.AdminPlayersClient(world.Fixture.Channel);
        var request = new KickPlayerRequest { CharacterId = character, Reason = reason };

        if (expected == StatusCode.OK)
        {
            request.Reason = new string('x', 201);
            expected = StatusCode.InvalidArgument;
        }

        var error = await Assert.ThrowsAsync<RpcException>(() => client.KickPlayerAsync(request, headers).ResponseAsync);

        Assert.Equal(expected, error.StatusCode);
    }

    [Fact]
    public async Task Broadcast_GivesTheTextToTheService_AndTheNumberOfRecipients()
    {
        await using var world = await World.CreateAsync();
        world.Broadcast.Recipients = 7;
        var headers = await world.LoginAsync(DomainAccountType.Administrator);

        var response = await new AdminOperations.AdminOperationsClient(world.Fixture.Channel).BroadcastAsync(new() { Text = "Restart in 5 minutes" }, headers);

        Assert.Equal(7u, response.Recipients);
        Assert.Equal(["Restart in 5 minutes"], world.Broadcast.Texts);
    }

    [Theory, InlineData(""), InlineData("   "), InlineData("a\0b"), InlineData("TOO-LONG")]
    public async Task Broadcast_BadText_IsInvalid(string text)
    {
        await using var world = await World.CreateAsync();
        var headers = await world.LoginAsync(DomainAccountType.Administrator);

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminOperations.AdminOperationsClient(world.Fixture.Channel)
                .BroadcastAsync(new() { Text = text == "TOO-LONG" ? new string('x', 201) : text }, headers)
                .ResponseAsync
        );

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
        Assert.Empty(world.Broadcast.Texts);
    }

    [Fact]
    public async Task Broadcast_ATextTheTransportCannotCarry_IsInvalid()
    {
        await using var world = await World.CreateAsync();
        world.Broadcast.Failure = new ArgumentException("too long for the transport");
        var headers = await world.LoginAsync(DomainAccountType.Administrator);

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminOperations.AdminOperationsClient(world.Fixture.Channel).BroadcastAsync(new() { Text = "hello" }, headers).ResponseAsync
        );

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task SaveWorld_SavesAndGivesTheDuration()
    {
        await using var world = await World.CreateAsync();
        world.Saves.Delay = TimeSpan.FromMilliseconds(60);
        var headers = await world.LoginAsync(DomainAccountType.Administrator);

        var response = await new AdminOperations.AdminOperationsClient(world.Fixture.Channel).SaveWorldAsync(new(), headers);

        Assert.Equal(1, world.Saves.Saves);
        Assert.InRange(response.DurationMs, 50u, 5000u);
    }

    [Fact]
    public async Task CreateSqlBackup_GivesTheFileNamesNotThePaths_AndTheFailures()
    {
        await using var world = await World.CreateAsync();
        world.Backups.Result = new()
        {
            Files = [new() { Database = "accounts", Path = "/private/backups/accounts-1.sql", Size = 2048 }],
            Failures = [new() { Database = "realm", Reason = "disk full" }]
        };
        var headers = await world.LoginAsync(DomainAccountType.Administrator);

        var response = await new AdminOperations.AdminOperationsClient(world.Fixture.Channel).CreateSqlBackupAsync(new(), headers);

        Assert.False(response.AlreadyRunning);
        var file = Assert.Single(response.Files);
        Assert.Equal(("accounts", "accounts-1.sql", 2048ul), (file.Database, file.FileName, file.SizeBytes));
        Assert.DoesNotContain("private", file.ToString());
        Assert.Equal(("realm", "disk full"), (response.Failures[0].Database, response.Failures[0].Reason));
    }

    [Fact]
    public async Task CreateSqlBackup_WhenOneIsRunning_SaysSo()
    {
        await using var world = await World.CreateAsync();
        world.Backups.Result = new() { AlreadyRunning = true };
        var headers = await world.LoginAsync(DomainAccountType.Administrator);

        var response = await new AdminOperations.AdminOperationsClient(world.Fixture.Channel).CreateSqlBackupAsync(new(), headers);

        Assert.True(response.AlreadyRunning);
        Assert.Empty(response.Files);
    }

    [Theory, InlineData(DomainAccountType.Regular), InlineData(DomainAccountType.GameMaster)]
    public async Task AdminOperations_AreForAdministratorsOnly(DomainAccountType role)
    {
        await using var world = await World.CreateAsync();
        var headers = await world.LoginAsync(role);
        var client = new AdminOperations.AdminOperationsClient(world.Fixture.Channel);

        Assert.Equal(StatusCode.PermissionDenied, (await Assert.ThrowsAsync<RpcException>(() => client.BroadcastAsync(new() { Text = "hi" }, headers).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.PermissionDenied, (await Assert.ThrowsAsync<RpcException>(() => client.SaveWorldAsync(new(), headers).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.PermissionDenied, (await Assert.ThrowsAsync<RpcException>(() => client.CreateSqlBackupAsync(new(), headers).ResponseAsync)).StatusCode);
        Assert.Empty(world.Broadcast.Texts);
        Assert.Equal(0, world.Saves.Saves);
        Assert.Equal(0, world.Backups.Backups);
    }

    [Fact]
    public async Task WorldServices_OnAHostWithoutThem_AreUnimplemented()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var result = await fixture.Backend.Accounts.Service.CreateAccountAsync(
            new() { Username = "admin", Password = fixture.Backend.Accounts.Password, AccountType = DomainAccountType.Administrator, CanAccessApi = true }
        );
        Assert.True(result.Success);
        var login = await new AdminLogin.AdminLoginClient(fixture.Channel).LoginAsync(new() { Username = "admin", Password = fixture.Backend.Accounts.Password });
        var headers = new Metadata { { "authorization", "Bearer " + login.AccessToken } };

        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(() => new AdminPlayers.AdminPlayersClient(fixture.Channel).ListOnlinePlayersAsync(new(), headers).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(() => new AdminOperations.AdminOperationsClient(fixture.Channel).SaveWorldAsync(new(), headers).ResponseAsync)).StatusCode);
    }

    [Fact]
    public async Task OnlyTheOperationsHaveALongerDeadline()
    {
        await using var world = await World.CreateAsync();
        var endpoints = world.Fixture.App.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        TimeSpan? DeadlineOf(string service)
        {
            return endpoints.First(endpoint => endpoint.DisplayName!.Contains(service)).Metadata.GetMetadata<AdminCallDeadline>()?.Duration;
        }

        Assert.Equal(TimeSpan.FromMinutes(5), DeadlineOf("AdminOperations"));
        Assert.Null(DeadlineOf("AdminPlayers"));
        Assert.Null(DeadlineOf("AdminAccounts"));
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly List<GameSession> _created = [];

        public AdminGrpcFixture Fixture { get; private set; } = null!;
        public BroadcastFixture Game { get; private set; } = null!;
        public FakeBroadcastService Broadcast { get; } = new();
        public FakeWorldSaveService Saves { get; } = new();
        public FakeSqlBackupService Backups { get; } = new();
        public uint CallerAccountId { get; private set; }
        public Dictionary<long, GameSession> Sessions { get; } = [];

        public static async Task<World> CreateAsync()
        {
            var world = new World { Game = await BroadcastFixture.CreateAsync() };
            world.Fixture = await AdminGrpcFixture.CreateAsync(
                Moongate.Server.Core.Types.Hosting.ServerMode.Standalone,
                world: new(world.Game.Sessions, world.Game.Mobiles, world.Game.Sender, world.Broadcast, world.Saves, world.Backups)
            );

            return world;
        }

        public async Task PlayerAsync(
            long characterId,
            uint accountId,
            DomainAccountType type,
            string name,
            MapType map = MapType.Trammel,
            Point3D? at = null,
            bool entered = true
        )
        {
            var session = await Game.AddAsync(characterId, entered, true, map);
            await Game.Network.ExecuteOnLoopAsync(() =>
                {
                    session.Set(SessionKeys.AccountId, new((uint)accountId));
                    session.Set(SessionKeys.AccountType, type);
                }
            );

            if (entered && Game.Mobiles.TryGet(new((uint)characterId), out var mobile))
            {
                mobile.Name = name;
                mobile.Location = at ?? mobile.Location;
            }

            _created.Add(session);
            Sessions[characterId] = session;
        }

        public async Task<Metadata> LoginAsync(DomainAccountType role)
        {
            var result = await Fixture.Backend.Accounts.Service.CreateAccountAsync(
                new() { Username = "admin", Password = Fixture.Backend.Accounts.Password, AccountType = role, CanAccessApi = true }
            );
            Assert.True(result.Success);
            CallerAccountId = result.Account!.Id.Value;
            var response = await new AdminLogin.AdminLoginClient(Fixture.Channel).LoginAsync(
                new() { Username = "admin", Password = Fixture.Backend.Accounts.Password }
            );

            return new() { { "authorization", "Bearer " + response.AccessToken } };
        }

        public async ValueTask DisposeAsync()
        {
            await Fixture.DisposeAsync();
            await Game.DisposeAsync();
        }
    }
}
