using System.Net;
using System.Text;
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Realms;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Server.Ultima;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Integration.Login;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class AccountServerListTests
{
    [Fact]
    public async Task LoginPacket_PostgreSqlAccount_FiltersRealmListByAccountType()
    {
        await using var accountFixture = await AccountServiceFixture.CreateAsync();
        var account = await accountFixture.SeedAsync();
        using var container = new Container();
        var connections = new ConnectionService();
        var network = new NetworkServiceStub(connections);
        var sessions = new LoginSessionService();
        var sender = new PacketSendService(connections);
        using var proof = new HandoffProofService(new byte[32]);
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        directory.RegisterLocal(new RealmDescriptor("visible", 1, "Visible", IPAddress.Loopback,
            2595, AccountType.Regular));
        directory.RegisterLocal(new RealmDescriptor("hidden", 2, "Hidden", IPAddress.Loopback,
            2596, AccountType.Administrator));
        container.RegisterInstance<ILoginSessionService>(sessions);
        container.RegisterInstance<ILoginPacketSendService>(sender);
        container.RegisterInstance<IHandoffProofService>(proof);
        container.RegisterInstance(accountFixture.Service);
        container.RegisterInstance(new LoginAccountFlow(accountFixture.Service, directory));
        container.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
        var dispatcher = new LoginPacketDispatchService(sessions,
            container.Resolve<LoginPacketHandlerRegistry>(), container);
        var server = new LoginServerService(network, connections, sessions, dispatcher, sender);
        await connections.StartAsync();
        await sender.StartAsync();
        await dispatcher.StartAsync();
        await server.StartAsync();
        using var connection = new ControlledNetworkConnection(11);

        try
        {
            network.Accept(connection);
            network.Receive(connection, LoginFrame(account.Username, accountFixture.Password));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await connection.ReadSentAsync(timeout.Token);

            Assert.Equal(0xA8, response[0]);
            Assert.Equal(1, response[5]);
            Assert.Equal(account.Id, sessions.TryGet(11, out var session) ? session.AccountId : Serial.Zero);
        }
        finally
        {
            await server.StopAsync();
            await dispatcher.StopAsync();
            await sender.StopAsync();
            await connections.StopAsync();
        }
    }

    [Theory, InlineData(true, 0xA8, 0x00), InlineData(false, 0x82, 0x04)]
    public async Task LoginPacket_SendsServerListOrCommunicationProblem(bool available,
        byte expectedOpcode, byte expectedReason)
    {
        using var container = new Container();
        var connections = new ConnectionService();
        var network = new NetworkServiceStub(connections);
        var sessions = new LoginSessionService();
        var sender = new PacketSendService(connections);
        using var proof = new HandoffProofService(new byte[32]);
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        if (available)
        {
            directory.RegisterLocal(new RealmDescriptor("local", 1, "Local", IPAddress.Loopback,
                2593, AccountType.Regular));
        }

        var accounts = new RecordingAccountService { LoginResult = new AccountEntity
        {
            Id = new Serial(42), AccountType = AccountType.Regular
        } };
        container.RegisterInstance<ILoginSessionService>(sessions);
        container.RegisterInstance<ILoginPacketSendService>(sender);
        container.RegisterInstance<IHandoffProofService>(proof);
        container.RegisterInstance<IAccountService>(accounts);
        container.RegisterInstance(new LoginAccountFlow(accounts, directory));
        container.RegisterLoginPacketHandler<LoginSeedPacket, LoginRoleSeedPacketHandler>();
        container.RegisterLoginPacketHandler<ClientVersionPacket, LoginRoleClientVersionPacketHandler>();
        container.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
        var dispatcher = new LoginPacketDispatchService(sessions,
            container.Resolve<LoginPacketHandlerRegistry>(), container);
        var server = new LoginServerService(network, connections, sessions, dispatcher, sender);
        await connections.StartAsync();
        await sender.StartAsync();
        await dispatcher.StartAsync();
        await server.StartAsync();
        using var connection = new ControlledNetworkConnection(1);

        try
        {
            network.Accept(connection);
            network.Receive(connection, LoginFrame("user", "password"));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = await connection.ReadSentAsync(timeout.Token);

            Assert.Equal(expectedOpcode, response[0]);
            if (available)
            {
                Assert.Equal(1, response[5]);
                Assert.Equal(1, response[7]);
                Assert.Equal(new byte[] { 1, 0, 0, 127 }, response[42..46]);
                Assert.Equal(new Serial(42), sessions.TryGet(1, out var session) ? session.AccountId : Serial.Zero);
            }
            else
            {
                Assert.Equal(expectedReason, response[1]);
                Assert.True(sessions.TryGet(1, out var session));
                Assert.Equal(Serial.Zero, session.AccountId);
            }
        }
        finally
        {
            await server.StopAsync();
            await dispatcher.StopAsync();
            await sender.StopAsync();
            await connections.StopAsync();
        }
    }

    private static byte[] LoginFrame(string account, string password)
    {
        var frame = new byte[62];
        frame[0] = 0x80;
        Encoding.ASCII.GetBytes(account).CopyTo(frame, 1);
        Encoding.ASCII.GetBytes(password).CopyTo(frame, 31);
        frame[^1] = 0xFF;
        return frame;
    }
}
