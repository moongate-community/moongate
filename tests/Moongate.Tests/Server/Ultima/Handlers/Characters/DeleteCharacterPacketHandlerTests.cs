using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Types.Characters;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Characters;

namespace Moongate.Tests.Server.Ultima.Handlers.Characters;

public sealed class DeleteCharacterPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_Deleted_SendsTheUpdatedListAndKeepsTheConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService
        {
            DeletionResult = CharacterDeletionResult.Deleted(
                new() { Id = new(3), Name = "Bran" },
                ["Aria", null, null, null, null]
            )
        };

        await new DeleteCharacterPacketHandler(characters).HandleAsync(
            context,
            new() { CharacterIndex = 3 },
            CancellationToken.None
        );

        Assert.Equal(3, characters.DeletionIndex);
        var list = Assert.IsType<CharacterListUpdatePacket>(Assert.Single(sender.Sent));
        Assert.Equal(["Aria", null, null, null, null], list.Characters);
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_Refused_SendsTheReasonAndKeepsTheConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService
        {
            DeletionResult = CharacterDeletionResult.Refused(CharacterDeleteResultType.CharacterBeingPlayed)
        };

        await new DeleteCharacterPacketHandler(characters).HandleAsync(
            context,
            new() { CharacterIndex = 0 },
            CancellationToken.None
        );

        Assert.Equal(
            CharacterDeleteResultType.CharacterBeingPlayed,
            Assert.IsType<CharacterDeleteResultPacket>(Assert.Single(sender.Sent)).Result
        );
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_ServiceFails_SendsRequestFailed()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService { Failure = new InvalidOperationException("database down") };

        await new DeleteCharacterPacketHandler(characters).HandleAsync(
            context,
            new() { CharacterIndex = 0 },
            CancellationToken.None
        );

        Assert.Equal(
            CharacterDeleteResultType.RequestFailed,
            Assert.IsType<CharacterDeleteResultPacket>(Assert.Single(sender.Sent)).Result
        );
    }

    [Fact]
    public async Task HandleAsync_WithoutAccount_DisconnectsWithoutAskingTheService()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _) = await Context(fixture, null);
        var characters = new RecordingCharacterService();

        await new DeleteCharacterPacketHandler(characters).HandleAsync(
            context,
            new() { CharacterIndex = 0 },
            CancellationToken.None
        );

        Assert.Null(characters.DeletionIndex);
        Assert.False(fixture.Client.IsConnected);
    }

    private static async Task<(PacketContext Context, StubPacketSendService Sender)> Context(
        SessionFixture fixture,
        Serial? account
    )
    {
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        if (account is { } id)
        {
            await context.RunOnGameLoopAsync(game => game.Set(SessionKeys.AccountId, id));
        }

        return (context, sender);
    }
}
