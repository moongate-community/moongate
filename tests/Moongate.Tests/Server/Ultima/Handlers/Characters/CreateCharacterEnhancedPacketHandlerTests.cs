using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Types.Characters;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Characters;

public sealed class CreateCharacterEnhancedPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_MapsThePacketToTheRequest()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService();

        await new CreateCharacterEnhancedPacketHandler(characters, new RecordingCharacterEnterWorldService()).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        Assert.Equal(new Serial(42), characters.CreatedFor);
        Assert.Equal(
            new CharacterCreationRequest
            {
                Slot = 3, Name = "Aria", Profession = 2, StartingCity = 1, Gender = GenderType.Female, Race = RaceType.Elf,
                Strength = 60, Dexterity = 20, Intelligence = 10, Skills = characters.Request!.Skills,
                SkinHue = new(0x0BF), HairStyle = 0x2FBF, HairHue = new(0x34), BeardStyle = 0, BeardHue = new(0),
                ShirtHue = new(0x20), PantsHue = new(0)
            },
            characters.Request
        );
        Assert.Equal(
            [(SkillType.Alchemy, (byte)50), (SkillType.Magery, (byte)50)],
            characters.Request.Skills.Select(s => (s.Skill, s.Value))
        );
        Assert.Equal(0, sender.SentCount);
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_Created_EntersTheWorldWithTheCharacterAndItsStartingItems()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, _) = await Context(fixture, new Serial(42));
        var character = new MobileEntity { Id = new(7), Name = "Aria" };
        var backpack = new ItemEntity { Id = new(0x40000001), MobileId = character.Id };
        var gold = new ItemEntity { Id = new(0x40000002), ContainerId = backpack.Id };
        var characters = new RecordingCharacterService
            { Result = CharacterCreationResult.Created(character, [backpack, gold]) };
        var enter = new RecordingCharacterEnterWorldService();

        await new CreateCharacterEnhancedPacketHandler(characters, enter).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        var (accountId, play) = Assert.Single(enter.Entered);
        Assert.Equal(new Serial(42), accountId);
        Assert.Same(character, play.Character);
        Assert.Same(backpack, Assert.Single(play.Equipment));
        Assert.Same(gold, Assert.Single(play.Contents));
    }

    [Fact]
    public async Task HandleAsync_AccountAlreadyInTheWorld_RefusesBeforeCreating()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService();
        var enter = new RecordingCharacterEnterWorldService { Allowed = false };

        await new CreateCharacterEnhancedPacketHandler(characters, enter).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        // Nothing is saved for a client that cannot enter: no slot is used up.
        Assert.Equal(0, characters.CreateCalls);
        Assert.Empty(enter.Entered);
        Assert.Equal(PopupMessageType.CharacterInWorld, Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent)).Type);
        Assert.False(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_Refused_DoesNotEnterTheWorld()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, _) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService
        {
            Result = CharacterCreationResult.Refused(CharacterCreationRefusalType.SlotUnavailable)
        };
        var enter = new RecordingCharacterEnterWorldService();

        await new CreateCharacterEnhancedPacketHandler(characters, enter).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        Assert.Empty(enter.Entered);
    }

    [Fact]
    public async Task HandleAsync_Refused_SendsThePopupAndDisconnects()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService
        {
            Result = CharacterCreationResult.Refused(CharacterCreationRefusalType.SlotUnavailable)
        };

        await new CreateCharacterEnhancedPacketHandler(characters, new RecordingCharacterEnterWorldService()).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        var popup = Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent));
        Assert.Equal(PopupMessageType.CharacterExists, popup.Type);
        Assert.False(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_TooManyCharacters_SendsTheSyncErrorPopup()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService
        {
            Result = CharacterCreationResult.Refused(CharacterCreationRefusalType.TooManyCharacters)
        };

        await new CreateCharacterEnhancedPacketHandler(characters, new RecordingCharacterEnterWorldService()).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        Assert.Equal(PopupMessageType.LoginSyncError, Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent)).Type);
    }

    [Fact]
    public async Task HandleAsync_ServiceFails_SendsCouldNotAttachAndDisconnects()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService { Failure = new InvalidOperationException("database down") };

        await new CreateCharacterEnhancedPacketHandler(characters, new RecordingCharacterEnterWorldService()).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        Assert.Equal(PopupMessageType.CouldNotAttach, Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent)).Type);
        Assert.False(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_WithoutAccount_DisconnectsWithoutCreating()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, _) = await Context(fixture, null);
        var characters = new RecordingCharacterService();

        await new CreateCharacterEnhancedPacketHandler(characters, new RecordingCharacterEnterWorldService()).HandleAsync(
            context,
            Packet(),
            CancellationToken.None
        );

        Assert.Equal(0, characters.CreateCalls);
        Assert.False(fixture.Client.IsConnected);
    }

    private static async Task<(PacketContext Context, SessionService Sessions, StubPacketSendService Sender)> Context(
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

        return (context, sessions, sender);
    }

    private static CreateCharacterEnhancedPacket Packet()
    {
        return new()
        {
            CharacterSlot = 3, Name = "Aria", Profession = 2, StartingCity = 1, Gender = GenderType.Female,
            Race = RaceType.Elf,
            Strength = 60, Dexterity = 20, Intelligence = 10, SkinHue = new(0x0BF),
            Skills = [new() { Skill = SkillType.Alchemy, Value = 50 }, new() { Skill = SkillType.Magery, Value = 50 }],
            HairHue = new(0x34), HairStyle = 0x2FBF, ShirtHue = new(0x20), ShirtStyle = 0x1517, FaceHue = new(0x0BF),
            FaceStyle = 0x3B44, BeardHue = new(0), BeardStyle = 0
        };
    }
}
