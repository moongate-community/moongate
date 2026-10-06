using System.Text;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.BulletinBoards;
using Moongate.Server.Ultima.Packets.BulletinBoards;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.BulletinBoards;

public sealed class BulletinBoardRequestPacketHandlerTests : IAsyncLifetime
{
    private static readonly Point3D BoardPlace = new(1492, 1607, 20);

    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemSerialPool _serials = new();
    private readonly BulletinBoardsConfig _config = new();
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero) };
    private readonly RecordingSpeechService _speech = new();

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate
                { Id = "bulletin_board", ItemId = new Serial(0x1E5E), ScriptId = BulletinBoardService.ScriptId },
            new ItemTemplate { Id = "chair", ItemId = new Serial(0x0B2E) }
        )
    );

    private BroadcastFixture _fixture = null!;
    private GameSession _ariaSession = null!;
    private GameSession _brunoSession = null!;
    private GameSession _staffSession = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bruno = null!;
    private MobileEntity _staff = null!;
    private ItemEntity _board = null!;
    private ItemEntity _other = null!;
    private BulletinBoardService _service = null!;
    private BulletinBoardRequestPacketHandler _handler = null!;

    // What was sent since the last call.
    private int _read;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        (_ariaSession, _aria) = await AddAsync(2, "Aria", AccountType.Regular, new Point3D(1493, 1608, 20));
        (_brunoSession, _bruno) = await AddAsync(3, "Bruno", AccountType.Regular, new Point3D(1491, 1606, 20));
        (_staffSession, _staff) = await AddAsync(4, "Giachi", AccountType.GameMaster, new Point3D(100, 100, 0));
        _board = Board(0x40000001, BoardPlace);
        _other = Board(0x40000002, new Point3D(1494, 1607, 20));

        for (uint index = 0; index < 64; index++)
        {
            _serials.Serials.Enqueue(new Serial(0x40001000 + index));
        }

        _service = new BulletinBoardService(
            new RecordingDataAccess<BulletinMessageEntity>(),
            _items,
            _templates,
            _serials,
            new RecordingTimerService(),
            _config,
            _clock,
            _fixture.Sender
        );
        _handler = new BulletinBoardRequestPacketHandler(_service, _items, _fixture.Mobiles, _fixture.Sender, _speech);
    }

    [Fact]
    public void Open_SendsTheBoardThenItsMessagesAsTheContentOfAContainer()
    {
        var thread = Posted(_aria, "Horse");
        var reply = Posted(_bruno, "Re: Horse", thread.Id);
        Posted(_bruno, "Elsewhere", board: _other);
        Sent();

        Assert.True(_service.Open(_board, _ariaSession));

        var sent = Sent();
        Assert.Equal(
            [typeof(BulletinBoardDisplayPacket), typeof(ContainerContentPacket)],
            sent.Select(packet => packet.GetType())
        );
        Assert.Equal(_board.Id, ((BulletinBoardDisplayPacket)sent[0]).Board);
        Assert.Contains("bulletin board", Encoding.ASCII.GetString(PacketCodec.Encode(sent[0])));
        var content = (ContainerContentPacket)sent[1];
        Assert.Equal(
            [
                (thread.Id, BulletinBoardService.MessageItemId, 1, _board.Id),
                (reply.Id, BulletinBoardService.MessageItemId, 1, _board.Id)
            ],
            content.Items.Select(item => (item.Serial, item.ItemId, item.Amount, item.Container))
        );
    }

    [Fact]
    public void Open_ShowsTheNameTheBoardWasGiven()
    {
        _board.Name = "Britain notices";

        _service.Open(_board, _ariaSession);

        Assert.Contains("Britain notices", Encoding.ASCII.GetString(PacketCodec.Encode(Sent()[0])));
    }

    [Fact]
    public void Open_AnEmptyBoard_StillSendsItsEmptyContent()
    {
        _service.Open(_board, _ariaSession);

        Assert.Empty(((ContainerContentPacket)Sent()[1]).Items);
    }

    [Fact]
    public void Open_TakesTheExpiredThreadsAwayFirst()
    {
        Posted(_aria, "Old");
        _clock.Advance(TimeSpan.FromDays(8));

        _service.Open(_board, _ariaSession);

        Assert.Empty(((ContainerContentPacket)Sent()[^1]).Items);
        Assert.Empty(_service.Messages);
    }

    [Fact]
    public void ARequestForASummary_GetsWhoWhatAndWhen_AndTheThread()
    {
        var thread = Posted(_aria, "Horse");
        var reply = Posted(_bruno, "Re: Horse", thread.Id);
        Sent();

        Handle(_ariaSession, "04", _board, reply.Id);

        var summary = Assert.IsType<BulletinBoardSummaryPacket>(Assert.Single(Sent()));
        Assert.Equal((_board.Id, reply.Id, thread.Id), (summary.Board, summary.Message, summary.Thread));
        var text = Encoding.ASCII.GetString(PacketCodec.Encode(summary));
        Assert.Contains("Bruno", text);
        Assert.Contains("Re: Horse", text);
        // In English whatever the culture of the server, and no time of day.
        Assert.Contains("Oct 05, 2026", text);
    }

    [Fact]
    public void ARequestForTheText_GetsTheLinesAndHowThePosterLooked()
    {
        var hat = new ItemEntity
            { Id = new Serial(0x40000100), TemplateId = "chair", ItemId = 0x1713, Amount = 1, Hue = new Hue(0x0021) };
        hat.Equip(_aria.Id, LayerType.Helm);
        _items.Add([hat]);
        var thread = Posted(_aria, "Horse");
        Sent();

        Handle(_brunoSession, "03", _board, thread.Id);

        var message = Assert.IsType<BulletinBoardMessagePacket>(Assert.Single(Sent()));
        Assert.Equal((_board.Id, thread.Id, _aria.Body), (message.Board, message.Message, message.Body));
        var bytes = PacketCodec.Encode(message);
        Assert.Contains("A fine mare.", Encoding.ASCII.GetString(bytes));
        // One piece worn: the hat and its hue.
        Assert.Contains("01" + "1713" + "0021", Convert.ToHexString(bytes));
    }

    [Fact]
    public void APost_PutsTheMessageOnTheBoard_AndShowsItInThePostersList()
    {
        Handle(_ariaSession, Post(_board, Serial.Zero, "Horse", "A fine mare."));

        var message = Assert.Single(_service.GetMessages(_board.Id));
        Assert.Equal(("Aria", "Horse"), (message.PosterName, message.Subject));
        var added = Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(Sent()));
        Assert.Equal(
            (message.Id, BulletinBoardService.MessageItemId, _board.Id),
            (added.Item.Serial, added.Item.ItemId, added.Item.Container)
        );
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void APostTooSoon_TellsThePosterHowLongItWaits_AndPostsNothing()
    {
        Handle(_ariaSession, Post(_board, Serial.Zero, "Horse", "x"));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Sent();

        Handle(_ariaSession, Post(_board, Serial.Zero, "Cart", "x"));

        Assert.Equal((_aria, "You must wait 90 seconds before posting again."), Assert.Single(_speech.Told));
        Assert.Single(_service.Messages);
        Assert.Empty(Sent());
    }

    [Fact]
    public void APostOnAFullBoard_TakesTheOldestThreadOutOfThePostersList()
    {
        _config.MaxMessages = 1;
        var old = Posted(_bruno, "Old");
        Sent();

        Handle(_ariaSession, Post(_board, Serial.Zero, "New", "x"));

        var sent = Sent();
        Assert.Equal(
            [typeof(RemoveEntityPacket), typeof(ContainerItemUpdatePacket)],
            sent.Select(packet => packet.GetType())
        );
        Assert.Equal(old.Id, ((RemoveEntityPacket)sent[0]).Serial);
    }

    [Fact]
    public void AnEmptyPost_DoesNothing()
    {
        Handle(_ariaSession, Post(_board, Serial.Zero, "", "x"));

        Assert.Empty(_service.Messages);
        Assert.Empty(Sent());
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void ARemovalByThePoster_TakesTheThreadAndItsRepliesOutOfTheList()
    {
        var thread = Posted(_aria, "Horse");
        var reply = Posted(_bruno, "Re: Horse", thread.Id);
        Sent();

        Handle(_ariaSession, "06", _board, thread.Id);

        Assert.Empty(_service.Messages);
        Assert.Equal(
            new[] { thread.Id, reply.Id }.Order(),
            Sent().Select(packet => Assert.IsType<RemoveEntityPacket>(packet).Serial).Order()
        );
    }

    [Fact]
    public void ARemovalBySomeoneElse_IsRefusedWithAWord()
    {
        var thread = Posted(_aria, "Horse");
        Sent();

        Handle(_brunoSession, "06", _board, thread.Id);

        Assert.Single(_service.Messages);
        Assert.Equal((_bruno, "That message is not yours."), Assert.Single(_speech.Told));
        Assert.Empty(Sent());
    }

    [Fact]
    public void AGameMaster_RemovesAnyMessage_FromAnywhere()
    {
        var thread = Posted(_aria, "Horse");
        Sent();

        Handle(_staffSession, "06", _board, thread.Id);

        Assert.Empty(_service.Messages);
        Assert.Single(Sent());
    }

    [Theory, InlineData("03"), InlineData("04"), InlineData("06")]
    public void AMessageOfAnotherBoard_AskedThroughThisOne_IsNotGiven(string command)
    {
        var elsewhere = Posted(_aria, "Elsewhere", board: _other);
        Sent();

        Handle(_ariaSession, command, _board, elsewhere.Id);

        Assert.Empty(Sent());
        Assert.Empty(_speech.Told);
        Assert.Single(_service.Messages);
    }

    [Theory, InlineData("03"), InlineData("04"), InlineData("06")]
    public void AMessageThatIsGone_IsNotGiven(string command)
    {
        Handle(_ariaSession, command, _board, new Serial(0x4000FFFF));

        Assert.Empty(Sent());
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void TooFarFromTheBoard_NothingIsAnswered_AndNothingIsPosted()
    {
        var thread = Posted(_aria, "Horse");
        _aria.Location = new Point3D(BoardPlace.X + 3, BoardPlace.Y, BoardPlace.Z);
        Sent();

        Handle(_ariaSession, "04", _board, thread.Id);
        Handle(_ariaSession, "03", _board, thread.Id);
        Handle(_ariaSession, "06", _board, thread.Id);
        Handle(_ariaSession, Post(_board, Serial.Zero, "Cart", "x"));

        Assert.Empty(Sent());
        Assert.Single(_service.Messages);
    }

    [Fact]
    public void TwoTilesAway_IsCloseEnough()
    {
        var thread = Posted(_bruno, "Horse");
        _aria.Location = new Point3D(BoardPlace.X + 2, BoardPlace.Y - 2, BoardPlace.Z);
        Sent();

        Handle(_ariaSession, "04", _board, thread.Id);

        Assert.Single(Sent());
    }

    [Fact]
    public void OnAnotherMap_NothingIsAnswered()
    {
        var thread = Posted(_aria, "Horse");
        _aria.Map = MapType.Felucca;
        Sent();

        Handle(_ariaSession, "04", _board, thread.Id);

        Assert.Empty(Sent());
    }

    [Fact]
    public void AGameMasterFarAway_IsAnswered()
    {
        var thread = Posted(_aria, "Horse");
        Sent();

        Handle(_staffSession, "04", _board, thread.Id);

        Assert.Single(Sent());
    }

    [Fact]
    public void WhatIsNotABoard_IsNotAnswered()
    {
        var chair = new ItemEntity { Id = new Serial(0x40000003), TemplateId = "chair", ItemId = 0x0B2E, Amount = 1 };
        chair.PlaceOnGround(MapType.Trammel, BoardPlace);
        _items.Add([chair]);

        Handle(_ariaSession, Post(chair, Serial.Zero, "Horse", "x"));
        Handle(_ariaSession, "04", new Serial(0x4000EEEE), new Serial(0x40001000));

        Assert.Empty(_service.Messages);
        Assert.Empty(Sent());
    }

    [Fact]
    public async Task ASessionWithNoCharacterInTheWorld_IsNotAnswered()
    {
        var thread = Posted(_aria, "Horse");
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_aria.Id));
        Sent();

        Handle(_ariaSession, "04", _board, thread.Id);

        Assert.Empty(Sent());
    }

    private IOutgoingPacket[] Sent()
    {
        var sent = _fixture.Sender.Sent.Skip(_read).ToArray();
        _read = _fixture.Sender.Sent.Count;

        return sent;
    }

    private BulletinMessageEntity Posted(
        MobileEntity poster, string subject, Serial replyTo = default, ItemEntity? board = null
    )
    {
        // The staff rank: these posts set the scene and do not wait.
        return _service.Post(board ?? _board, poster, AccountType.GameMaster, replyTo, subject, ["A fine mare."]).Message!;
    }

    private void Handle(GameSession session, string command, ItemEntity board, Serial message)
    {
        Handle(session, command, board.Id, message);
    }

    private void Handle(GameSession session, string command, Serial board, Serial message)
    {
        Handle(
            session,
            Convert.FromHexString("71000C" + command + board.Value.ToString("X8") + message.Value.ToString("X8"))
        );
    }

    private void Handle(GameSession session, byte[] data)
    {
        Assert.True(BulletinBoardRequestPacket.TryParse(data, out var packet));
        _handler.Handle(session, packet);
    }

    private static byte[] Post(ItemEntity board, Serial replyTo, string subject, string line)
    {
        var body = new List<byte> { 0x05 };
        body.AddRange(Convert.FromHexString(board.Id.Value.ToString("X8") + replyTo.Value.ToString("X8")));

        foreach (var text in new[] { subject, null, line })
        {
            if (text is null)
            {
                body.Add(1);

                continue;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            body.Add((byte)(bytes.Length + 1));
            body.AddRange(bytes);
            body.Add(0);
        }

        var length = body.Count + 3;

        return [0x71, (byte)(length >> 8), (byte)length, .. body];
    }

    private ItemEntity Board(uint serial, Point3D place)
    {
        var board = new ItemEntity { Id = new Serial(serial), TemplateId = "bulletin_board", ItemId = 0x1E5E, Amount = 1 };
        board.PlaceOnGround(MapType.Trammel, place);
        _items.Add([board]);

        return board;
    }

    private async Task<(GameSession, MobileEntity)> AddAsync(long id, string name, AccountType rank, Point3D place)
    {
        var session = await _fixture.AddAsync(id);
        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountType, rank));
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)id), out var mobile));
        mobile.Name = name;
        mobile.Body = 0x0190;
        mobile.Location = place;

        return (session, mobile);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
