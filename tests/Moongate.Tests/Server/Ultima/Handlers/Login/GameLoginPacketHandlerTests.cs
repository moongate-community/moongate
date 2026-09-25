using System.Net;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class GameLoginPacketHandlerTests
{
    private const uint AuthKey = 0x12345678;

    [Fact]
    public async Task HandleAsync_ValidTicket_AssociatesTicketIdentityOnGameLoop()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        session.NetworkSession.SetSeed(AuthKey);
        var store = new RecordingHandoffStore { Result = Handoff() };
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await new GameLoginPacketHandler(Realm(), store).HandleAsync(
            context,
            new(AuthKey, "user", "password"),
            CancellationToken.None
        );

        Assert.Equal(new(42), session.AccountId);
        Assert.Equal(AccountType.GameMaster, session.AccountType);
        Assert.Equal(1, store.RedeemCalls);
        Assert.Equal("realm", store.RealmId);
        Assert.Equal(AuthKey, store.AuthKey);
        Assert.Equal(0, sender.SentCount);
    }

    [Theory, InlineData(null, AuthKey), InlineData(0x23456789u, AuthKey)]
    public async Task HandleAsync_MissingOrMismatchedSeed_ClosesWithoutRedeeming(uint? seed, uint packetKey)
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);

        if (seed is { } value)
        {
            session.NetworkSession.SetSeed(value);
        }

        var store = new RecordingHandoffStore { Result = Handoff() };
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await new GameLoginPacketHandler(Realm(), store).HandleAsync(
            context,
            new(packetKey, "user", "password"),
            CancellationToken.None
        );

        Assert.Equal(0, store.RedeemCalls);
        Assert.Equal(Serial.Zero, session.AccountId);
        Assert.Equal(1, sender.SentCount);
        Assert.False(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_InvalidTicket_DeniesWithoutIdentity()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        session.NetworkSession.SetSeed(AuthKey);
        var store = new RecordingHandoffStore();
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await new GameLoginPacketHandler(Realm(), store).HandleAsync(
            context,
            new(AuthKey, "user", "wrong-password"),
            CancellationToken.None
        );

        Assert.Equal(1, store.RedeemCalls);
        Assert.Equal(Serial.Zero, session.AccountId);
        Assert.Equal(1, sender.SentCount);
        Assert.False(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_StoreFailure_DeniesWithoutIdentity()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        session.NetworkSession.SetSeed(AuthKey);
        var store = new RecordingHandoffStore { Failure = new InvalidOperationException("Redis unavailable") };
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await new GameLoginPacketHandler(Realm(), store).HandleAsync(
            context,
            new(AuthKey, "user", "password"),
            CancellationToken.None
        );

        Assert.Equal(Serial.Zero, session.AccountId);
        Assert.Equal(1, sender.SentCount);
        Assert.False(fixture.Client.IsConnected);
    }

    private static RealmInstance Realm()
    {
        return new(
                new(
                    "realm",
                    1,
                    "Realm",
                    IPAddress.Loopback,
                    2595,
                    AccountType.Regular
                ),
                Guid.Parse("74e13e2c-dab8-4acf-8613-362362b0e83a")
            );
    }

    private static PendingHandoff Handoff()
    {
        return new(new(42), AccountType.GameMaster, "user", "realm", Realm().InstanceId, "7.0.117");
    }

    private sealed class RecordingHandoffStore : IGameHandoffStore
    {
        public PendingHandoff? Result { get; init; }
        public Exception? Failure { get; init; }
        public int RedeemCalls { get; private set; }
        public string? RealmId { get; private set; }
        public uint AuthKey { get; private set; }

        public ValueTask<uint> IssueAsync(
            PendingHandoff handoff,
            ReadOnlyMemory<byte> credentialKey,
            CancellationToken token = default
        )
        {
            throw new NotSupportedException();
        }

        public ValueTask<PendingHandoff?> RedeemAsync(
            string realmId,
            Guid instanceId,
            uint authKey,
            string username,
            string password,
            CancellationToken token = default
        )
        {
            RedeemCalls++;
            RealmId = realmId;
            AuthKey = authKey;

            if (Failure is not null)
            {
                throw Failure;
            }

            return ValueTask.FromResult(Result);
        }

        public ValueTask RevokeAsync(string realmId, uint authKey, CancellationToken token = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
