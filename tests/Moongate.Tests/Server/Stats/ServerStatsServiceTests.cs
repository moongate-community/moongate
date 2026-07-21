using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Stats;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.Server;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Stats;

public sealed class ServerStatsServiceTests
{
    [Fact]
    public void Current_BeforeTheFirstRefresh_IsTheEmptySnapshot()
    {
        var (service, _, _) = Build();

        Assert.Same(ServerStatsSnapshot.Empty, service.Current);
        Assert.Equal(DateTimeOffset.MinValue, service.Current.GeneratedAt);
    }

    [Fact]
    public void Refresh_CountsAccountsCharactersAndTemplates()
    {
        var (service, persistence, _) = Build();
        SeedAccount(persistence, "tom", isActive: true, characters: 2);
        SeedAccount(persistence, "ann", isActive: false, characters: 1);

        service.Refresh();

        Assert.Equal(2, service.Current.Accounts);
        Assert.Equal(1, service.Current.ActiveAccounts);
        Assert.Equal(3, service.Current.Characters);
        Assert.Equal(1, service.Current.ItemTemplates);
        Assert.Equal(1, service.Current.MobileTemplates);
    }

    [Fact]
    public void Refresh_CountsNpcsAsMobilesThatNoAccountOwns()
    {
        var (service, persistence, _) = Build();
        SeedAccount(persistence, "tom", isActive: true, characters: 2);

        // Two more mobiles owned by nobody: what an NPC looks like in the store.
        persistence.Store<MobileEntity>().UpsertAsync(new() { Name = "orc" }).WaitSync();
        persistence.Store<MobileEntity>().UpsertAsync(new() { Name = "rat" }).WaitSync();

        service.Refresh();

        Assert.Equal(2, service.Current.Npcs);
    }

    [Fact]
    public void Refresh_NpcCountNeverGoesNegative()
    {
        var (service, persistence, _) = Build();

        // An account claiming characters whose mobiles are gone: orphaned ids would drive the
        // subtraction below zero.
        var account = new AccountEntity
        {
            Username = "tom",
            PasswordHash = "x",
            ActivationToken = string.Empty,
            IsActive = true,
            MobileIds = [new(900), new(901), new(902)]
        };
        persistence.Store<AccountEntity>().UpsertAsync(account).WaitSync();

        service.Refresh();

        Assert.Equal(0, service.Current.Npcs);
    }

    [Fact]
    public void Refresh_CountsWorldItems()
    {
        var (service, persistence, _) = Build();
        persistence.Store<ItemEntity>().UpsertAsync(new() { Name = "sword" }).WaitSync();
        persistence.Store<ItemEntity>().UpsertAsync(new() { Name = "shield" }).WaitSync();

        service.Refresh();

        Assert.Equal(2, service.Current.WorldItems);
    }

    [Fact]
    public void Refresh_OnlinePlayers_CountsOnlyCharactersASessionIsPlaying()
    {
        var (service, persistence, sessions) = Build();
        var account = SeedAccount(persistence, "tom", isActive: true, characters: 2);
        sessions.Played.Add(account.MobileIds[0]);
        sessions.Count = 5;

        service.Refresh();

        Assert.Equal(1, service.Current.OnlinePlayers);
        Assert.Equal(5, service.Current.Connections);
    }

    [Fact]
    public void Refresh_WhenAReadThrows_KeepsThePreviousSnapshot()
    {
        var (service, _, sessions) = Build();

        service.Refresh();
        var previous = service.Current;
        Assert.NotEqual(DateTimeOffset.MinValue, previous.GeneratedAt);

        sessions.ThrowOnCount = true;
        service.Refresh();

        // No exception escapes — it would poison the game loop — and the last good numbers survive.
        Assert.Same(previous, service.Current);
    }

    [Fact]
    public async Task StartAsync_RegistersTheRepeatingRefreshTimer_AndStopAsyncCancelsIt()
    {
        var loop = new StubGameLoopContext();
        var service = new ServerStatsService(
            new FakePersistenceService(),
            new StubSessionManager(),
            new ItemTemplateService(),
            new MobileTemplateService(),
            loop,
            TimeProvider.System,
            new() { UltimaDirectory = "/tmp", StatsRefreshSeconds = 45 }
        );

        await service.StartAsync();

        Assert.True(loop.Repeating.ContainsKey("server-stats"));
        Assert.Equal(TimeSpan.FromSeconds(45), loop.RepeatingInterval);

        // The registered callback is the refresh itself: firing it publishes a snapshot.
        loop.Repeating["server-stats"]();
        Assert.NotEqual(DateTimeOffset.MinValue, service.Current.GeneratedAt);

        await service.StopAsync();
        Assert.False(loop.Repeating.ContainsKey("server-stats"));
    }

    /// <summary>A service over fake persistence, one item template and one mobile template.</summary>
    private static (ServerStatsService Service, FakePersistenceService Persistence, StubSessionManager Sessions) Build()
    {
        var persistence = new FakePersistenceService();
        var sessions = new StubSessionManager();

        var itemTemplates = new ItemTemplateService();
        itemTemplates.Register(new() { Id = "sword" });

        var mobileTemplates = new MobileTemplateService();
        mobileTemplates.Register(new() { Id = "orc" });

        var service = new ServerStatsService(
            persistence,
            sessions,
            itemTemplates,
            mobileTemplates,
            new StubGameLoopContext(),
            TimeProvider.System,
            new() { UltimaDirectory = "/tmp" }
        );

        return (service, persistence, sessions);
    }

    /// <summary>Stores an account owning <paramref name="characters" /> freshly created mobiles.</summary>
    private static AccountEntity SeedAccount(
        FakePersistenceService persistence,
        string username,
        bool isActive,
        int characters
    )
    {
        var account = new AccountEntity
        {
            Username = username,
            PasswordHash = "x",
            ActivationToken = string.Empty,
            IsActive = isActive
        };

        for (var index = 0; index < characters; index++)
        {
            var mobile = new MobileEntity { Name = $"{username}-{index}" };
            persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            account.MobileIds.Add(mobile.Id);
        }

        persistence.Store<AccountEntity>().UpsertAsync(account).WaitSync();

        return account;
    }
}
