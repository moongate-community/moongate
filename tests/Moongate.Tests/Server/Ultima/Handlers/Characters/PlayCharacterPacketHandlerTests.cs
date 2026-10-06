using Moongate.Tests.TestSupport.Scripting;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Data.Config;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
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
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Characters;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Characters;

public sealed class PlayCharacterPacketHandlerTests : IDisposable
{
    private readonly Container _events = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingWorldViewService _view = new();
    private StubCharacterLeaveWorldService _leaves = new();
    private SessionService _sessions = null!;
    private readonly List<(CharacterEnteredWorldEvent Event, int SentBefore)> _entered = [];
    private readonly RecordingMotdService _motd = new();

    public void Dispose()
    {
        _events.Dispose();
    }

    [Fact]
    public async Task HandleAsync_SendsTheLightOfTheCharactersTimeOfDay()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var characters = new RecordingCharacterService { ForPlay = Aria() };
        var light = new LightService(
            new StubClockService { Time = new GameTime(1, 0) },
            _sessions,
            _mobiles,
            sender,
            new RecordingTimerService(),
            fixture.Loop,
            new WorldConfig(),
            new StubDataLoaderService()
        );

        await Handler(characters, sender, light: light).HandleAsync(context, Packet(2), CancellationToken.None);

        Assert.Equal(12, Assert.Single(sender.Sent.OfType<GlobalLightLevelPacket>()).Level);
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
                typeof(MobileIncomingPacket), typeof(MobileStatusPacket), typeof(StatLockInfoPacket), typeof(WarModePacket),
                typeof(LoginCompletePacket), typeof(CurrentTimePacket)
            ],
            sender.Sent.Select(packet => packet.GetType())
        );
        Assert.Equal(2, characters.PlayIndex);
        Assert.Equal(new Serial(0x00000002), session.CharacterId);
        Assert.True(_mobiles.IsInWorld(new Serial(0x00000002)));
        Assert.True(_mobiles.TryGet(new Serial(0x00000002), out var live));
        Assert.Same(characters.ForPlay!.Character, live);
        var entered = Assert.Single(_entered);
        Assert.Equal(sender.Sent.Count, entered.SentBefore);
        Assert.Equal([12], _motd.SentBefore);
        Assert.Equal("Aria", entered.Event.Character.Name);
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task HandleAsync_AfterTheEnterWorldSequence_ShowsThePlayerToTheWorldView()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, session, sender) = await Context(fixture, new Serial(42));
        var sentBefore = -1;
        _view.OnCall = _ => sentBefore = sender.Sent.Count;

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Equal([$"Entered 2 {session.SessionId}"], _view.Calls);
        Assert.Equal(sender.Sent.Count, sentBefore);
    }

    [Fact]
    public async Task HandleAsync_TheSessionClosesDuringTheSequence_NeitherShowsThePlayerNorPublishesTheEvent()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        sender.OnSent = packet =>
        {
            if (packet is CurrentTimePacket)
            {
                fixture.Client.CloseAsync().GetAwaiter().GetResult();
            }
        };

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Empty(_view.Calls);
        Assert.Empty(_entered);
    }

    [Fact]
    public async Task HandleAsync_TellsTheMurderServiceToForgetTheCountsOfWhoComesBack()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var murders = new RecordingMurderService();

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender, murders: murders)
              .HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Equal(["Restore 2"], murders.Calls);
    }

    [Fact]
    public async Task HandleAsync_SendsTheSeasonOfTheSeasonService()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender, seasons: new StubSeasonService { Here = SeasonType.Fall })
              .HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Equal(SeasonType.Fall, sender.Sent.OfType<SeasonChangePacket>().Single().Season);
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
    public async Task HandleAsync_TheCharacterEntersTheWorldOnTheLoopWithTheSessionCharacter()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var mobiles = new LoopCheckingMobileService(_mobiles, fixture.Loop);

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender, mobiles)
            .HandleAsync(context, Packet(0), CancellationToken.None);

        // Entering on the loop, with the session's character, orders it before any session retirement.
        Assert.Equal([true], mobiles.EnteredOnLoop);
    }

    [Fact]
    public async Task HandleAsync_KeepsTheWornItemsAndTheirContentsLive_ButShowsOnlyTheWornOnes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Equal([new Serial(0x40000001), new Serial(0x40000002)], _items.Items.Select(item => item.Id).Order());
        Assert.DoesNotContain(
            sender.Sent.OfType<MobileIncomingPacket>().Single().Equipment,
            entry => entry.Serial == new Serial(0x40000002)
        );
    }

    // The character's rows are as its last save left them: another player may have taken an item since.
    [Fact]
    public async Task HandleAsync_AnItemAlreadyLiveElsewhere_IsNotLoadedAgainWithTheCharacter()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var taken = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "item", ItemId = 0x0EED, Amount = 1 };
        taken.PlaceOnGround(MapType.Trammel, new Point3D(100, 100, 0));
        _items.Add([taken]);

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.True(_items.TryGet(new Serial(0x40000002), out var live));
        Assert.Same(taken, live);
        Assert.NotNull(live.GroundLocation);
        Assert.True(_items.TryGet(new Serial(0x40000001), out _));
    }

    [Fact]
    public async Task HandleAsync_AWornItemAlreadyLiveElsewhere_IsNeitherLoadedNorShownOnTheCharacter()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var taken = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "item", ItemId = 0x0E75, Amount = 1 };
        taken.PlaceOnGround(MapType.Trammel, new Point3D(100, 100, 0));
        _items.Add([taken]);

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.True(_items.TryGet(new Serial(0x40000001), out var live));
        Assert.Same(taken, live);
        Assert.DoesNotContain(
            sender.Sent.OfType<MobileIncomingPacket>().Single().Equipment,
            entry => entry.Serial == new Serial(0x40000001)
        );
    }

    [Fact]
    public async Task HandleAsync_WaitsForTheAccountsLeaveSavesBeforeLoading()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        _leaves = new StubCharacterLeaveWorldService(false);
        var characters = new RecordingCharacterService { ForPlay = Aria() };

        var handling = Handler(characters, sender).HandleAsync(context, Packet(0), CancellationToken.None).AsTask();
        await Task.Delay(50);

        Assert.Null(characters.PlayIndex);
        Assert.Equal([new Serial(42)], _leaves.WaitedFor);
        _leaves.Pending.SetResult();
        await handling.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, characters.PlayIndex);
    }

    [Fact]
    public async Task HandleAsync_TheCharacterFacesTheWayItWasSaved()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, _, sender) = await Context(fixture, new Serial(42));
        var play = Aria();
        play.Character.Direction = DirectionType.West;

        await Handler(new RecordingCharacterService { ForPlay = play }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Equal(DirectionType.West, sender.Sent.OfType<LoginConfirmPacket>().Single().Direction);
        Assert.Equal(DirectionType.West, sender.Sent.OfType<MobileUpdatePacket>().Single().Direction);
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
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task HandleAsync_TheSessionAlreadyPlaysACharacter_RefusesWithCharacterInWorld()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, session, sender) = await Context(fixture, new Serial(42));
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(7)));

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        // A second entry would replace the session's character and leave the first one in the world for ever.
        Assert.Equal(PopupMessageType.CharacterInWorld, Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent)).Type);
        Assert.Equal(new Serial(7), session.CharacterId);
        Assert.False(_mobiles.IsInWorld(new Serial(2)));
        Assert.Empty(_entered);
    }

    [Fact]
    public async Task HandleAsync_AnotherCharacterOfTheAccountInTheWorld_RefusesWithCharacterInWorld()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var (context, session, sender) = await Context(fixture, new Serial(42));
        using var otherClient = new ControlledNetworkConnection(9_001);
        var other = _sessions.GetOrCreate(otherClient);
        await fixture.ExecuteOnLoopAsync(() =>
            {
                other.Set(SessionKeys.AccountId, new Serial(42));
                other.Set(SessionKeys.CharacterId, new Serial(7));
            }
        );

        await Handler(new RecordingCharacterService { ForPlay = Aria() }, sender).HandleAsync(context, Packet(0), CancellationToken.None);

        Assert.Equal(PopupMessageType.CharacterInWorld, Assert.IsType<PopupMessagePacket>(Assert.Single(sender.Sent)).Type);
        Assert.Equal(Serial.Zero, session.CharacterId);
        Assert.False(_mobiles.IsInWorld(new Serial(2)));
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

    private PlayCharacterPacketHandler Handler(
        RecordingCharacterService characters,
        StubPacketSendService sender,
        IMobileService? mobiles = null,
        ILightService? light = null,
        ISeasonService? seasons = null,
        IMurderService? murders = null
    )
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

        _motd.Sender = sender;
        return new(
            characters,
            _leaves,
            new CharacterEnterWorldService(mobiles ?? _mobiles, _items, loaders, bus, _sessions, _view, _motd, light, seasons, murders: murders)
        );
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

        var dagger = new ItemEntity { Id = new(0x40000002), TemplateId = "dagger", ItemId = 0x0F52, Amount = 1 };
        dagger.PutInContainer(backpack.Id, new Point2D(44, 65));

        return new(aria, [backpack], [dagger]);
    }

    private static PlayCharacterPacket Packet(int index)
    {
        return new() { Name = "Aria", ClientFlags = ClientFlags.None, LoginCount = 1, CharacterIndex = index };
    }

    private async Task<(PacketContext Context, GameSession Session, StubPacketSendService Sender)> Context(
        SessionFixture fixture,
        Serial? account
    )
    {
        var sessions = new SessionService(fixture.Loop);
        _sessions = sessions;
        var session = sessions.GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        if (account is { } id)
        {
            await context.RunOnGameLoopAsync(game => game.Set(SessionKeys.AccountId, id));
        }

        return (context, session, sender);
    }

    private sealed class RecordingMotdService : IMotdService
    {
        public StubPacketSendService? Sender { get; set; }
        public List<int> SentBefore { get; } = [];

        public ValueTask SendAsync(PacketContext context, MobileEntity character, CancellationToken cancellationToken)
        {
            SentBefore.Add(Sender!.Sent.Count);
            return ValueTask.CompletedTask;
        }
    }
}
