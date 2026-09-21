using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Integration.Sessions;

public sealed class SessionServiceTests
{
    [Fact]
    public async Task Clear_RegisteredSessions_RemovesMembershipWithoutClosingClients()
    {
        await using var firstFixture = await SessionFixture.CreateAsync();
        await using var secondFixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(firstFixture.Loop);
        service.GetOrCreate(firstFixture.Client);
        service.GetOrCreate(secondFixture.Client);

        service.Clear();

        Assert.Equal(0, service.Count);
        Assert.Empty(service.GetAll());
        Assert.True(firstFixture.Client.IsConnected);
        Assert.True(secondFixture.Client.IsConnected);
    }

    [Fact]
    public async Task GetOrCreate_ConcurrentCallsForSameClient_ReturnSameSession()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(fixture.Loop);

        var sessions = await Task.WhenAll(
                           Enumerable.Range(0, 32).Select(_ => Task.Run(() => service.GetOrCreate(fixture.Client)))
                       );

        Assert.All(sessions, session => Assert.Same(sessions[0], session));
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public async Task GetOrCreate_DetachedRegisteredSession_ReturnsSameDetachedInstance()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(fixture.Loop);
        var session = service.GetOrCreate(fixture.Client);
        session.NetworkSession.DetachClient();

        var repeated = service.GetOrCreate(fixture.Client);

        Assert.Same(session, repeated);
        Assert.Null(repeated.NetworkSession.Client);
    }

    [Fact]
    public async Task GetOrCreate_IndependentClients_RegistersIndependentSessions()
    {
        await using var firstFixture = await SessionFixture.CreateAsync();
        await using var secondFixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(firstFixture.Loop);

        var first = service.GetOrCreate(firstFixture.Client);
        var second = service.GetOrCreate(secondFixture.Client);

        Assert.NotSame(first, second);
        Assert.Equal(2, service.Count);
        Assert.True(service.TryGet(first.SessionId, out var foundFirst));
        Assert.Same(first, foundFirst);
        Assert.True(service.TryGet(second.SessionId, out var foundSecond));
        Assert.Same(second, foundSecond);
    }

    [Fact]
    public async Task GetOrCreate_NullClient_ThrowsArgumentNullException()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(fixture.Loop);

        Assert.Throws<ArgumentNullException>(() => service.GetOrCreate(null!));
    }

    [Fact]
    public async Task RegisterAndRemove_SeparateClientKeysConcurrently_HasCorrectQuiescentCount()
    {
        var fixtures = new List<SessionFixture>();

        try
        {
            foreach (var _ in Enumerable.Range(0, 8))
            {
                fixtures.Add(await SessionFixture.CreateAsync());
            }

            ISessionService service = new SessionService(fixtures[0].Loop);

            var sessions = await Task.WhenAll(
                               fixtures.Select(fixture => Task.Run(() => service.GetOrCreate(fixture.Client)))
                           );

            Assert.Equal(fixtures.Count, service.Count);

            var removals = await Task.WhenAll(sessions.Select(session => Task.Run(() => service.Remove(session.SessionId))));

            Assert.All(removals, Assert.True);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            await Task.WhenAll(fixtures.Select(fixture => fixture.DisposeAsync().AsTask()));
        }
    }

    [Fact]
    public async Task RegisterServices_SessionServiceResolutionAcrossScope_ReturnsSingleton()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        bootstrap.RegisterServices(
            services =>
            {
                services.RegisterInstance<IGameLoopService>(fixture.Loop);

                return services.RegisterMoongateService<ISessionService, SessionService>();
            }
        );

        var rootInstance = container.Resolve<ISessionService>();
        using var scope = container.OpenScope();
        var scopedInstance = scope.Resolve<ISessionService>();

        Assert.Same(rootInstance, scopedInstance);
    }

    [Fact]
    public async Task Remove_RegisteredSession_PreservesSnapshotAndConnectedClient()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(fixture.Loop);
        var session = service.GetOrCreate(fixture.Client);
        var snapshot = service.GetAll();

        var firstRemoval = service.Remove(session.SessionId);
        var secondRemoval = service.Remove(session.SessionId);

        Assert.True(firstRemoval);
        Assert.False(secondRemoval);
        Assert.Single(snapshot);
        Assert.Same(session, Assert.Single(snapshot));
        Assert.Empty(service.GetAll());
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task TryGetByCharacterId_AssociationChanges_ReturnsCurrentMatchOnly()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(fixture.Loop);
        var session = service.GetOrCreate(fixture.Client);
        var firstCharacterId = new Serial(42);
        var secondCharacterId = new Serial(84);

        Assert.False(service.TryGetByCharacterId(Serial.Zero, out var zeroSession));
        Assert.Null(zeroSession);
        Assert.False(service.TryGetByCharacterId(firstCharacterId, out var missingSession));
        Assert.Null(missingSession);

        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(firstCharacterId));

        Assert.True(service.TryGetByCharacterId(firstCharacterId, out var firstMatch));
        Assert.Same(session, firstMatch);

        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(secondCharacterId));

        Assert.False(service.TryGetByCharacterId(firstCharacterId, out var changedSession));
        Assert.Null(changedSession);
        Assert.True(service.TryGetByCharacterId(secondCharacterId, out var secondMatch));
        Assert.Same(session, secondMatch);

        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(Serial.Zero));

        Assert.False(service.TryGetByCharacterId(secondCharacterId, out var clearedSession));
        Assert.Null(clearedSession);
    }

    [Fact]
    public async Task TryGet_UnknownSessionId_ReturnsFalseAndNull()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        ISessionService service = new SessionService(fixture.Loop);

        var found = service.TryGet(long.MinValue, out var session);

        Assert.False(found);
        Assert.Null(session);
    }
}
