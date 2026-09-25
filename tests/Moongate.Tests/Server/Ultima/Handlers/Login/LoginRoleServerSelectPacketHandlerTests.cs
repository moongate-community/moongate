using System.Net;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Login;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Realms;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class LoginRoleServerSelectPacketHandlerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task HandleAsync_UnauthenticatedSelectionIsDeniedWithoutTicket()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = sessions.GetOrCreate(connection);
        var catalog = new ControlledRealmCatalog { Result = Realm() };
        var store = new RecordingGameHandoffStore();
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleServerSelectPacketHandler(sessions, catalog, store, sender);

        await handler.HandleAsync(session, new(1), CancellationToken.None);

        Assert.Equal(0, store.IssueCount);
        Assert.Null(catalog.RequestedIndex);
        Assert.Equal(
            LoginDeniedReason.InvalidCredentials,
            Assert.IsType<LoginDeniedPacket>(Assert.Single(sender.Sent)).Reason
        );
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_MissingRealmIsDeniedWithoutTicket()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = Authenticate(sessions, connection);
        var catalog = new ControlledRealmCatalog();
        var store = new RecordingGameHandoffStore();
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleServerSelectPacketHandler(sessions, catalog, store, sender);

        await handler.HandleAsync(session, new(1), CancellationToken.None);

        Assert.Equal((ushort)1, catalog.RequestedIndex);
        Assert.Equal(AccountType.Regular, catalog.RequestedAccountType);
        Assert.Equal(0, store.IssueCount);
        Assert.Equal(
            LoginDeniedReason.CommunicationProblem,
            Assert.IsType<LoginDeniedPacket>(Assert.Single(sender.Sent)).Reason
        );
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_RealmAboveAccountLevelIsDenied()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = Authenticate(sessions, connection);
        var catalog = new ControlledRealmCatalog { Result = Realm(AccountType.Administrator) };
        var store = new RecordingGameHandoffStore();
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleServerSelectPacketHandler(sessions, catalog, store, sender);

        await handler.HandleAsync(session, new(1), CancellationToken.None);

        Assert.Equal(0, store.IssueCount);
        Assert.Equal(
            LoginDeniedReason.CommunicationProblem,
            Assert.IsType<LoginDeniedPacket>(Assert.Single(sender.Sent)).Reason
        );
    }

    [Fact]
    public async Task HandleAsync_SelectedRealmIssuesTicketAndFlushesRedirectBeforeClose()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = Authenticate(sessions, connection);
        session.NetworkSession.SetClientVersion(ClientVersion.Parse("7.0.117"));
        var realm = Realm();
        var catalog = new ControlledRealmCatalog { Result = realm };
        var store = new RecordingGameHandoffStore();
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleServerSelectPacketHandler(sessions, catalog, store, sender);

        await handler.HandleAsync(session, new(1), CancellationToken.None);

        var redirect = Assert.IsType<ServerRedirectPacket>(Assert.Single(sender.Sent));
        Assert.Equal(realm.Descriptor.Address, redirect.Address);
        Assert.Equal(realm.Descriptor.Port, redirect.Port);
        Assert.Equal(store.NextAuthKey, redirect.AuthKey);
        Assert.Equal(
            new(
                new(42),
                AccountType.Regular,
                "Alice",
                "realm-a",
                realm.InstanceId,
                ClientVersion.Parse("7.0.117")
            ),
            store.IssuedHandoff
        );
        Assert.Equal(Enumerable.Range(0, 32).Select(value => (byte)value), store.IssuedKeySnapshot);
        Assert.All(store.IssuedKeyBuffer.ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Empty(store.Revoked);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_ReusedSessionDuringRealmLookupDoesNotIssueTicket()
    {
        var sessions = new LoginSessionService();
        using var first = new ControlledNetworkConnection(1);
        var original = Authenticate(sessions, first);
        var pending = new TaskCompletionSource<RealmInstance?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new ControlledRealmCatalog { PendingResult = pending };
        var store = new RecordingGameHandoffStore();
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleServerSelectPacketHandler(sessions, catalog, store, sender);
        var handling = handler.HandleAsync(original, new(1), CancellationToken.None).AsTask();
        await catalog.Entered.Task.WaitAsync(Timeout);
        Assert.True(sessions.Remove(original));
        using var replacementConnection = new ControlledNetworkConnection(1);
        var replacement = sessions.GetOrCreate(replacementConnection);
        pending.SetResult(Realm());

        await handling.WaitAsync(Timeout);

        Assert.Equal(0, store.IssueCount);
        Assert.Empty(sender.Sent);
        Assert.True(sessions.IsCurrent(replacement));
    }

    [Fact]
    public async Task HandleAsync_ReusedSessionAfterTicketIssueRevokesIt()
    {
        var sessions = new LoginSessionService();
        using var first = new ControlledNetworkConnection(1);
        var original = Authenticate(sessions, first);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new RecordingGameHandoffStore { IssueGate = gate.Task };
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleServerSelectPacketHandler(
            sessions,
            new ControlledRealmCatalog { Result = Realm() },
            store,
            sender
        );
        var handling = handler.HandleAsync(original, new(1), CancellationToken.None).AsTask();
        await store.IssueEntered.Task.WaitAsync(Timeout);
        Assert.True(sessions.Remove(original));
        using var replacementConnection = new ControlledNetworkConnection(1);
        var replacement = sessions.GetOrCreate(replacementConnection);
        gate.SetResult();

        await handling.WaitAsync(Timeout);

        Assert.Equal(("realm-a", store.NextAuthKey), Assert.Single(store.Revoked));
        Assert.Empty(sender.Sent);
        Assert.True(sessions.IsCurrent(replacement));
    }

    [Fact]
    public async Task HandleAsync_FailedRedirectSendRevokesTicketAndClosesConnection()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = Authenticate(sessions, connection);
        var store = new RecordingGameHandoffStore();
        var sender = new RecordingLoginPacketSender { TerminalResult = false };
        var handler = new LoginRoleServerSelectPacketHandler(
            sessions,
            new ControlledRealmCatalog { Result = Realm() },
            store,
            sender
        );

        await handler.HandleAsync(session, new(1), CancellationToken.None);

        Assert.Equal(("realm-a", store.NextAuthKey), Assert.Single(store.Revoked));
        Assert.False(connection.IsConnected);
    }

    private static LoginSession Authenticate(LoginSessionService sessions, ControlledNetworkConnection connection)
    {
        var session = sessions.GetOrCreate(connection);
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        Assert.True(session.TrySetAccount(new(42), AccountType.Regular, "Alice", key));
        Array.Clear(key);

        return session;
    }

    private static RealmInstance Realm(AccountType minimumAccountType = AccountType.Regular)
    {
        return new(
            new(
                "realm-a",
                1,
                "Realm A",
                IPAddress.Parse("127.0.0.9"),
                2595,
                minimumAccountType
            ),
            Guid.Parse("234a81d2-c5d4-48cf-a05f-494e541c94d4")
        );
    }
}
