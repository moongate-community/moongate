using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Characters;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Characters;

public sealed class PlayCharacterPacketHandlerTests : IDisposable
{
    private readonly Container _events = new();
    private readonly MobileService _mobiles = new();
    private readonly List<(CharacterEnteredWorldEvent Event, int SentBefore)> _entered = [];

    public void Dispose()
    {
        _events.Dispose();
    }

    [Fact]
    public async Task HandleAsync_SendsTheEnterWorldSequenceInOrder_ThenPublishesTheEvent()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, session, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService { ForPlay = Aria() };

        await Handler(characters, sender).HandleAsync(context, Packet(2), CancellationToken.None);

        Assert.Equal(
            [
                typeof(LoginConfirmPacket), typeof(MapChangePacket), typeof(SeasonChangePacket),
                typeof(GlobalLightLevelPacket), typeof(PersonalLightLevelPacket), typeof(MobileUpdatePacket),
                typeof(MobileIncomingPacket), typeof(MobileStatusPacket), typeof(WarModePacket),
                typeof(LoginCompletePacket), typeof(CurrentTimePacket)
            ],
            sender.Sent.Select(packet => packet.GetType())
        );
        Assert.Equal(2, characters.PlayIndex);
        Assert.Equal(new Serial(0x00000002), session.CharacterId);
        Assert.True(_mobiles.IsInWorld(new Serial(0x00000002)));
        var entered = Assert.Single(_entered);
        Assert.Equal(sender.Sent.Count, entered.SentBefore);
        Assert.Equal("Aria", entered.Event.Character.Name);
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_TellsTheClientTheMapSizeAndSeason()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        var confirm = sender.Sent.OfType<LoginConfirmPacket>().Single();
        Assert.Equal((7168, 4096), (confirm.MapWidth, confirm.MapHeight));
        Assert.Equal(SeasonType.Winter, sender.Sent.OfType<SeasonChangePacket>().Single().Season);
    }

    [Fact]
    public async Task HandleAsync_ShowsWornItemsHairAndBeard_WithVirtualSerials()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));

        await Handler(new RecordingCharacterService { ForPlay = Aria(beard: 0x203E) }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        var incoming = sender.Sent.OfType<MobileIncomingPacket>().Single();
        Assert.Equal(new Serial(0x40000001), incoming.Equipment.Single(entry => entry.Layer == LayerType.Backpack).Serial);
        var hair = incoming.Equipment.Single(entry => entry.Layer == LayerType.Hair);
        Assert.Equal((_mobiles.HairSerial(new Serial(2)), 0x203C, (ushort)0x044E), (hair.Serial, hair.ItemId, hair.Hue.Value));
        Assert.Equal(_mobiles.BeardSerial(new Serial(2)), incoming.Equipment.Single(entry => entry.Layer == LayerType.FacialHair).Serial);
    }

    [Fact]
    public async Task HandleAsync_NoHairOrBeard_SendsNoEntryForThem()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));

        await Handler(new RecordingCharacterService { ForPlay = Aria(hair: 0) }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        var incoming = sender.Sent.OfType<MobileIncomingPacket>().Single();
        Assert.DoesNotContain(incoming.Equipment, entry => entry.Layer is LayerType.Hair or LayerType.FacialHair);
    }

    [Fact]
    public async Task HandleAsync_NoCharacterAtThatPosition_SendsThePopupAndDisconnects()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, session, sender) = await Context(fixture, new Serial(42));

        await Handler(new RecordingCharacterService(), sender).HandleAsync(context, Packet(4), CancellationToken.None);

        Assert.Equal(PopupMessageType.CharacterDoesNotExist, Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent)).Type);
        Assert.Equal(Serial.Zero, session.CharacterId);
        Assert.False(fixture.Client.IsConnected);
        Assert.Empty(_entered);
    }

    [Fact]
    public async Task HandleAsync_WithoutAccount_DisconnectsWithoutLoading()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, null);
        var characters = new RecordingCharacterService { ForPlay = Aria() };

        await Handler(characters, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Null(characters.PlayIndex);
        Assert.False(fixture.Client.IsConnected);
    }

    private PlayCharacterPacketHandler Handler(RecordingCharacterService characters, StubPacketSendService sender)
    {
        _events.RegisterMoongateEventBus();
        var bus = _events.Resolve<IMoongateEventBus>();
        bus.Subscribe<CharacterEnteredWorldEvent>((evt, _) =>
            {
                _entered.Add((evt, sender.Sent.Count));

                return Task.CompletedTask;
            }
        );
        var loaders = new StubDataLoaderService().With(
            new MapContent { Map = MapType.Trammel, Size = new Point2D(7168, 4096), Season = SeasonType.Winter, Name = "Trammel" }
        );

        return new(characters, _mobiles, loaders, bus);
    }

    private static CharacterForPlay Aria(int hair = 0x203C, int beard = 0)
    {
        var aria = new MobileEntity
        {
            Id = new(0x00000002), AccountId = new Serial(42), Name = "Aria", Body = 0x0191, Gender = GenderType.Female,
            Race = RaceType.Human, SkinHue = new(0x83EA), HairStyle = hair, HairHue = new(0x044E), BeardStyle = beard,
            BeardHue = new(0x044E), Strength = 60, Dexterity = 20, Intelligence = 10, Hits = 60, HitsMax = 60,
            Stamina = 20, StaminaMax = 20, Mana = 10, ManaMax = 10, Map = MapType.Trammel,
            Location = new Point3D(1496, 1628, 10)
        };
        var backpack = new ItemEntity { Id = new(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(aria.Id, LayerType.Backpack);

        return new(aria, [backpack]);
    }

    private static PlayCharacterPacket Packet(int index)
    {
        return new() { Name = "Aria", ClientFlags = ClientFlags.None, LoginCount = 1, CharacterIndex = index };
    }

    private static async Task<(PacketContext Context, GameSession Session, StubPacketSendService Sender)> Context(
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

        return (context, session, sender);
    }
}
