using System.Net;
using System.Security.Cryptography;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Realms;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Realms;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class LoginRoleAccountPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidAccountStoresDerivedCredentialKeyForRealmSelection()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = sessions.GetOrCreate(connection);
        using var proof = new HandoffProofService(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var handler = new LoginRoleAccountPacketHandler(
            sessions,
            new RecordingLoginPacketSender(),
            new(new RecordingAccountService { LoginResult = Account() }, Directory()),
            proof
        );

        await handler.HandleAsync(
            session,
            new("user", "password", 0xFF),
            CancellationToken.None
        );

        Assert.True(
            session.TryGetAuthenticatedAccount(
                out var accountId,
                out var accountType,
                out var username,
                out var credentialKey
            )
        );
        Assert.Equal(new(42), accountId);
        Assert.Equal(AccountType.Regular, accountType);
        Assert.Equal("user", username);
        var expected = proof.DeriveCredentialKey("user", "password");
        Assert.Equal(expected, credentialKey);
        CryptographicOperations.ZeroMemory(expected);
        CryptographicOperations.ZeroMemory(credentialKey);
    }

    [Fact]
    public async Task HandleAsync_ValidAccount_SendsEligibleServerList()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = sessions.GetOrCreate(connection);
        var sender = new RecordingLoginPacketSender();
        var accounts = new RecordingAccountService { LoginResult = Account() };
        using var proof = new HandoffProofService(new byte[32]);
        var handler = new LoginRoleAccountPacketHandler(
            sessions,
            sender,
            new(accounts, Directory()),
            proof
        );

        await handler.HandleAsync(
            session,
            new("user", "password", 0xFF),
            CancellationToken.None
        );

        var list = Assert.IsType<ServerListPacket>(Assert.Single(sender.Sent));
        Assert.Equal((ushort)1, Assert.Single(list.Servers).ServerIndex);
        Assert.Equal(new(42), session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_NoRealm_SendsCommunicationProblemWithoutIdentity()
    {
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        var session = sessions.GetOrCreate(connection);
        var sender = new RecordingLoginPacketSender();
        using var proof = new HandoffProofService(new byte[32]);
        var handler = new LoginRoleAccountPacketHandler(
            sessions,
            sender,
            new(
                new RecordingAccountService { LoginResult = Account() },
                new StubRealmCatalog()
            ),
            proof
        );

        await handler.HandleAsync(
            session,
            new("user", "password", 0xFF),
            CancellationToken.None
        );

        Assert.Equal(
            LoginDeniedReason.CommunicationProblem,
            Assert.IsType<LoginDeniedPacket>(Assert.Single(sender.Sent)).Reason
        );
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
        using var proof = new HandoffProofService(new byte[32]);
        var handler = new LoginRoleAccountPacketHandler(
            sessions,
            sender,
            new(accounts, Directory()),
            proof
        );
        var pending = handler.HandleAsync(
                original,
                new("user", "password", 0xFF),
                CancellationToken.None
            )
            .AsTask();
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
    {
        return new() { Id = new(42), AccountType = AccountType.Regular };
    }

    private static StubRealmCatalog Directory()
    {
        return new(
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
}
