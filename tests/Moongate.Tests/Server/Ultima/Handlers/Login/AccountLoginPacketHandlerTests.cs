using System.Net;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
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
        var handler = new AccountLoginPacketHandler(
            new LoginAccountFlow(new RecordingAccountService(), Directory()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new AccountLoginPacket("tester", "secret", 0xFF), CancellationToken.None);

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
        var handler = new AccountLoginPacketHandler(
            new LoginAccountFlow(new RecordingAccountService(), Directory()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await handler.HandleAsync(context, new AccountLoginPacket("tester", "secret", 0xFF), CancellationToken.None);

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
        var accounts = new RecordingAccountService { LoginResult =
            new AccountEntity { Id = new Serial(42), AccountType = AccountType.Regular } };
        var handler = new AccountLoginPacketHandler(new LoginAccountFlow(accounts, Directory()));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new AccountLoginPacket("tester", "secret", 0xFF), CancellationToken.None);
            var bytes = await middleware.ReadAsync();
            Assert.Equal(0xA8, bytes[0]);
            Assert.Equal(1, bytes[5]);
            Assert.Equal(new Serial(42), session.AccountId);
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
        var accounts = new RecordingAccountService { LoginResult =
            new AccountEntity { Id = new Serial(42), AccountType = AccountType.Regular } };
        var handler = new AccountLoginPacketHandler(
            new LoginAccountFlow(accounts, new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15))));
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new AccountLoginPacket("tester", "secret", 0xFF), CancellationToken.None);
            Assert.Equal(new byte[] { 0x82, 0x04 }, await middleware.ReadAsync());
            Assert.Equal(Serial.Zero, session.AccountId);
        }
        finally
        {
            await sender.StopAsync();
        }
    }

    private static RealmDirectoryService Directory()
    {
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        directory.RegisterLocal(new RealmDescriptor("local", 1, "Local", IPAddress.Loopback, 2593,
            AccountType.Regular));
        return directory;
    }

}
