using System.Net;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Realms;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class AccountLoginPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_UnknownAccount_SendsInvalidCredentials()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware();
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var handler = new AccountLoginPacketHandler(new(new RecordingAccountService(), Directory()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new("tester", "secret", 0xFF), CancellationToken.None);

            Assert.Equal(new byte[] { 0x82, 0x03 }, await middleware.ReadAsync());
            Assert.True(fixture.Client.IsConnected);
        }
        finally
        {
            await sender.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task HandleAsync_RejectedReply_ClosesConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var handler = new AccountLoginPacketHandler(new(new RecordingAccountService(), Directory()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await handler.HandleAsync(context, new("tester", "secret", 0xFF), CancellationToken.None);

        await fixture.Client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Client.IsConnected);
        await sender.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task HandleAsync_ValidAccount_SendsServerList()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware();
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var accounts = new RecordingAccountService
        {
            LoginResult =
                new() { Id = new(42), AccountType = AccountType.Regular }
        };
        var handler = new AccountLoginPacketHandler(new(accounts, Directory()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new("tester", "secret", 0xFF), CancellationToken.None);
            var bytes = await middleware.ReadAsync();
            Assert.Equal(0xA8, bytes[0]);
            Assert.Equal(1, bytes[5]);
            Assert.Equal(new(42), session.AccountId);
        }
        finally
        {
            await sender.StopAsync();
        }
    }

    [Fact]
    public async Task HandleAsync_NoAvailableRealm_DeniesWithoutAuthenticating()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware();
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var accounts = new RecordingAccountService
        {
            LoginResult =
                new() { Id = new(42), AccountType = AccountType.Regular }
        };
        var handler = new AccountLoginPacketHandler(new(accounts, new StubRealmCatalog()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new("tester", "secret", 0xFF), CancellationToken.None);
            Assert.Equal(new byte[] { 0x82, 0x04 }, await middleware.ReadAsync());
            Assert.Equal(Serial.Zero, session.AccountId);
        }
        finally
        {
            await sender.StopAsync();
        }
    }

    private static StubRealmCatalog Directory()
        => new(
            new RealmDescriptor(
                "local",
                1,
                "Local",
                IPAddress.Loopback,
                2593,
                AccountType.Regular
            )
        );
}
