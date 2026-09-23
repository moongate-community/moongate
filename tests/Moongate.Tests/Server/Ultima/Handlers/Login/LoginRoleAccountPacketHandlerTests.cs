using System.Net;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Realms;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class LoginRoleAccountPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidAccount_SendsEligibleServerList()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = sessions.GetOrCreate(connection);
        var sender = new RecordingLoginPacketSender();
        var accounts = new RecordingAccountService { LoginResult = Account() };
        var handler = new LoginRoleAccountPacketHandler(sessions, sender,
            new LoginAccountFlow(accounts, Directory()));

        await handler.HandleAsync(session, new AccountLoginPacket("user", "password", 0xFF),
            CancellationToken.None);

        var list = Assert.IsType<ServerListPacket>(Assert.Single(sender.Sent));
        Assert.Equal((ushort)1, Assert.Single(list.Servers).ServerIndex);
        Assert.Equal(new Serial(42), session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_NoRealm_SendsCommunicationProblemWithoutIdentity()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = sessions.GetOrCreate(connection);
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleAccountPacketHandler(sessions, sender,
            new LoginAccountFlow(new RecordingAccountService { LoginResult = Account() },
                new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15))));

        await handler.HandleAsync(session, new AccountLoginPacket("user", "password", 0xFF),
            CancellationToken.None);

        Assert.Equal(LoginDeniedReason.CommunicationProblem,
            Assert.IsType<LoginDeniedPacket>(Assert.Single(sender.Sent)).Reason);
        Assert.Equal(Serial.Zero, session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_DisconnectedDuringAuthentication_DoesNotTouchReplacementSlot()
    {
        var sessions = new LoginSessionService();
        using var first = new ControlledNetworkConnection(1);
        var original = sessions.GetOrCreate(first);
        var accounts = new BlockingAccountService();
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRoleAccountPacketHandler(sessions, sender,
            new LoginAccountFlow(accounts, Directory()));
        var pending = handler.HandleAsync(original, new AccountLoginPacket("user", "password", 0xFF),
            CancellationToken.None).AsTask();
        await accounts.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(sessions.Remove(original));
        using var replacementConnection = new ControlledNetworkConnection(1);
        var replacement = sessions.GetOrCreate(replacementConnection);
        accounts.Release(Account());

        await pending.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(sender.Sent);
        Assert.Equal(Serial.Zero, replacement.AccountId);
    }

    private static AccountEntity Account()
        => new() { Id = new Serial(42), AccountType = AccountType.Regular };

    private static RealmDirectoryService Directory()
    {
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        directory.RegisterLocal(new RealmDescriptor("local", 1, "Local", IPAddress.Loopback, 2593,
            AccountType.Regular));
        return directory;
    }
}
