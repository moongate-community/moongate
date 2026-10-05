using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.BulletinBoards;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class BulletinBoardServiceTests
{
    private const long Minute = 60_000;
    private const long Day = 86_400_000;

    private readonly RecordingDataAccess<BulletinMessageEntity> _table = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemSerialPool _serials = new();
    private readonly RecordingTimerService _timers = new();
    private readonly BulletinBoardsConfig _config = new();
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero) };
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "bulletin_board", ItemId = new Serial(0x1E5E), ScriptId = BulletinBoardService.ScriptId },
            new ItemTemplate { Id = "chair", ItemId = new Serial(0x0B2E) }
        )
    );

    private readonly MobileEntity _aria = new() { Id = new Serial(2), Name = "Aria", Body = 0x0191, SkinHue = new Hue(0x83EA), Map = MapType.Trammel };
    private readonly MobileEntity _bruno = new() { Id = new Serial(3), Name = "Bruno", Body = 0x0190, Map = MapType.Trammel };
    private readonly MobileEntity _staff = new() { Id = new Serial(4), Name = "Giachi", Body = 0x0190, Map = MapType.Trammel };

    private const uint FirstSerial = 0x40001000;
    private readonly ItemEntity _board;
    private readonly ItemEntity _other;
    private readonly BulletinBoardService _service;

    public BulletinBoardServiceTests()
    {
        _board = Board(0x40000001);
        _other = Board(0x40000002);
        _service = Create();
    }

    [Fact]
    public void IsBoard_ForAnItemWhoseTemplateHasTheScriptOfABoard()
    {
        var chair = new ItemEntity { Id = new Serial(0x40000003), TemplateId = "chair", ItemId = 0x0B2E, Amount = 1 };

        Assert.True(_service.IsBoard(_board));
        Assert.False(_service.IsBoard(chair));
        Assert.False(_service.IsBoard(new ItemEntity { Id = new Serial(0x40000004), TemplateId = "gone" }));
    }

    [Fact]
    public void Post_ANewThread_KeepsWhoWroteWhatAndWhen()
    {
        var result = Post(_aria, "Horse for sale", ["A fine mare.", "", "Ask Aria in Britain."]);

        Assert.Equal(BulletinPostResultType.Ok, result.Type);
        var message = Assert.Single(_service.Messages);
        Assert.Same(message, result.Message);
        var now = _clock.Now.ToUnixTimeMilliseconds();
        Assert.Equal(
            (new Serial(0x40001000), _board.Id, Serial.Zero, _aria.Id, "Aria", "Horse for sale", now, now),
            (message.Id, message.BoardId, message.ThreadId, message.PosterId, message.PosterName, message.Subject, message.PostedAt, message.LastReplyAt)
        );
        Assert.Equal(["A fine mare.", "", "Ask Aria in Britain."], message.Lines());
        Assert.Same(message, _service.GetMessage(message.Id));
    }

    [Fact]
    public void Post_KeepsHowThePosterLooked_ItsBodyItsHueAndWhatItWore_NotItsBankBox()
    {
        Wear(_aria, 0x1F03, 0x0021, LayerType.OuterTorso);
        Wear(_aria, 0x203B, 0x044E, LayerType.Hair);
        Wear(_aria, 0x0E7C, 0, LayerType.Bank);

        var message = Post(_aria, "Horse", ["x"]).Message!;

        Assert.Equal((0x0191, 0x83EA), (message.PosterBody, message.PosterHue));
        // In the order of the layers: the hair is on 11, the robe on 22; the bank box is not worn.
        Assert.Equal([new BulletinEquipment(0x203B, 0x044E), new BulletinEquipment(0x1F03, 0x0021)], BulletinEquipment.Parse(message.PosterEquipment));
    }

    [Fact]
    public void APostOnOneBoard_IsNotOnAnother()
    {
        Post(_aria, "Horse", ["x"]);

        Assert.Single(_service.GetMessages(_board.Id));
        Assert.Empty(_service.GetMessages(_other.Id));
    }

    [Theory, InlineData(""), InlineData("   "), InlineData("\t\n")]
    public void Post_WithoutASubject_PostsNothing(string subject)
    {
        Assert.Equal(BulletinPostResultType.Empty, Post(_aria, subject, ["text"]).Type);
        Assert.Empty(_service.Messages);
    }

    [Fact]
    public void Post_WithNoLineOfText_PostsNothing()
    {
        Assert.Equal(BulletinPostResultType.Empty, Post(_aria, "Horse", []).Type);
        Assert.Equal(BulletinPostResultType.Empty, Post(_aria, "Horse", ["", "  "]).Type);
        // No serial was spent on them: the next post has the first of the pool.
        Assert.Equal(new Serial(0x40001000), Post(_aria, "Horse", ["x"]).Message!.Id);
    }

    [Fact]
    public void Post_CutsTheSubjectTheLinesAndTheirNumber_AndTakesControlCharactersOut()
    {
        var lines = Enumerable.Range(0, 40).Select(index => index == 0 ? new string('y', 200) : "l\u0007ine\t" + index).ToArray();

        var message = Post(_aria, "  " + new string('x', 100) + "  ", lines).Message!;

        Assert.Equal(new string('x', BulletinBoardService.MaxSubject), message.Subject);
        var kept = message.Lines();
        Assert.Equal(BulletinBoardService.MaxLines, kept.Count);
        Assert.Equal(new string('y', BulletinBoardService.MaxLine), kept[0]);
        Assert.Equal("line1", kept[1]);
    }

    [Fact]
    public void Post_CutsWithoutSplittingACharacterInTwo()
    {
        // The sixtieth place falls in the middle of a character of two code units.
        var message = Post(_aria, new string('x', 59) + "\U0001F600" + "tail", ["x"]).Message!;

        Assert.Equal(new string('x', 59), message.Subject);
    }

    [Fact]
    public void Post_DropsTheEmptyLinesAtTheEnd_AndKeepsThoseInTheMiddle()
    {
        var message = Post(_aria, "Horse", ["one", "", "two", "", "  "]).Message!;

        Assert.Equal(["one", "", "two"], message.Lines());
    }

    [Fact]
    public void Post_AReply_GoesUnderItsThread_AndMovesTheLastReplyOfTheThread()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromHours(3));

        var reply = Post(_bruno, "Re: Horse", ["How much?"], thread.Id).Message!;

        Assert.Equal(thread.Id, reply.ThreadId);
        Assert.Equal(_clock.Now.ToUnixTimeMilliseconds(), thread.LastReplyAt);
        Assert.NotEqual(thread.PostedAt, thread.LastReplyAt);
    }

    [Fact]
    public void Post_AReplyToAReply_GoesUnderTheFirstMessageOfTheThread()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        var reply = Post(_bruno, "Re: Horse", ["How much?"], thread.Id).Message!;

        var second = Post(_staff, "Re: Re: Horse", ["Too much."], reply.Id).Message!;

        Assert.Equal(thread.Id, second.ThreadId);
    }

    [Fact]
    public void Post_AReplyToAMessageThatIsGone_OrOfAnotherBoard_StartsAThread()
    {
        var elsewhere = _service.Post(_other, _bruno, AccountType.Regular, Serial.Zero, "Elsewhere", ["x"]).Message!;

        var gone = Post(_aria, "Horse", ["x"], new Serial(0x4000FFFF)).Message!;
        var wrongBoard = Post(_staff, "Cart", ["x"], elsewhere.Id).Message!;

        Assert.True(gone.IsThread);
        Assert.True(wrongBoard.IsThread);
        Assert.Equal(_board.Id, wrongBoard.BoardId);
    }

    [Fact]
    public void GetMessages_GivesTheThreadsOldestFirst_EachFollowedByItsReplies()
    {
        var first = Post(_aria, "First", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromMinutes(10));
        var second = Post(_bruno, "Second", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromMinutes(10));
        var replyToFirst = Post(_staff, "Re: First", ["x"], first.Id).Message!;
        _clock.Advance(TimeSpan.FromMinutes(10));
        var replyToSecond = Post(_aria, "Re: Second", ["x"], second.Id).Message!;
        _clock.Advance(TimeSpan.FromMinutes(10));
        var later = Post(_bruno, "Re: First again", ["x"], first.Id).Message!;

        Assert.Equal(
            [first.Id, replyToFirst.Id, later.Id, second.Id, replyToSecond.Id],
            _service.GetMessages(_board.Id).Select(message => message.Id)
        );
    }

    [Fact]
    public void Post_ASecondThreadTooSoon_IsRefused_AndSaysHowLongIsLeft()
    {
        Post(_aria, "Horse", ["x"]);
        _clock.Advance(TimeSpan.FromSeconds(50));

        var result = Post(_aria, "Cart", ["x"]);

        Assert.Equal((BulletinPostResultType.TooSoon, 70), (result.Type, result.WaitSeconds));
        Assert.Single(_service.Messages);

        _clock.Advance(TimeSpan.FromSeconds(70));
        Assert.Equal(BulletinPostResultType.Ok, Post(_aria, "Cart", ["x"]).Type);
    }

    [Fact]
    public void Post_AReplyTooSoonAfterAnyPost_IsRefused()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromSeconds(10));

        var result = Post(_aria, "Re: Horse", ["Still here."], thread.Id);

        Assert.Equal((BulletinPostResultType.TooSoon, 20), (result.Type, result.WaitSeconds));

        _clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(BulletinPostResultType.Ok, Post(_aria, "Re: Horse", ["Still here."], thread.Id).Type);
    }

    [Fact]
    public void TheWait_IsOfOneCharacterOnOneBoard()
    {
        Post(_aria, "Horse", ["x"]);

        Assert.Equal(BulletinPostResultType.Ok, Post(_bruno, "Cart", ["x"]).Type);
        Assert.Equal(BulletinPostResultType.Ok, _service.Post(_other, _aria, AccountType.Regular, Serial.Zero, "Cart", ["x"]).Type);
    }

    [Theory, InlineData(AccountType.GameMaster), InlineData(AccountType.Administrator)]
    public void TheStaff_DoesNotWait(AccountType rank)
    {
        _service.Post(_board, _staff, rank, Serial.Zero, "One", ["x"]);

        Assert.Equal(BulletinPostResultType.Ok, _service.Post(_board, _staff, rank, Serial.Zero, "Two", ["x"]).Type);
    }

    [Fact]
    public void Post_WhenNoSerialIsReady_IsBusy_AndPostsNothing()
    {
        var service = Create(serials: new StubItemSerialPool());

        Assert.Equal(BulletinPostResultType.Busy, service.Post(_board, _aria, AccountType.Regular, Serial.Zero, "Horse", ["x"]).Type);
        Assert.Empty(service.Messages);
    }

    [Fact]
    public void Remove_AReply_TakesOnlyIt()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        var reply = Post(_bruno, "Re: Horse", ["x"], thread.Id).Message!;

        Assert.Equal([reply.Id], _service.Remove(reply.Id));

        Assert.Equal([thread.Id], _service.Messages.Select(message => message.Id));
        Assert.Null(_service.GetMessage(reply.Id));
    }

    [Fact]
    public void Remove_AFirstMessage_TakesItsReplies()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        var reply = Post(_bruno, "Re: Horse", ["x"], thread.Id).Message!;
        var other = Post(_staff, "Cart", ["x"]).Message!;

        var gone = _service.Remove(thread.Id);

        Assert.Equal(new[] { thread.Id, reply.Id }.Order(), gone.Order());
        Assert.Equal([other.Id], _service.Messages.Select(message => message.Id));
    }

    [Fact]
    public void Remove_WhatIsNotThere_TakesNothing()
    {
        Assert.Empty(_service.Remove(new Serial(0x4000FFFF)));
    }

    [Fact]
    public void CanRemove_ThePosterAndTheStaff_NobodyElse()
    {
        var message = Post(_aria, "Horse", ["x"]).Message!;

        Assert.True(_service.CanRemove(message, _aria, AccountType.Regular));
        Assert.False(_service.CanRemove(message, _bruno, AccountType.Regular));
        Assert.True(_service.CanRemove(message, _staff, AccountType.GameMaster));
    }

    [Fact]
    public void Expire_TakesTheThreadsWhoseLastReplyIsOlderThanTheDays_WithTheirReplies()
    {
        var old = Post(_aria, "Old", ["x"]).Message!;
        var oldReply = Post(_bruno, "Re: Old", ["x"], old.Id).Message!;
        _clock.Advance(TimeSpan.FromDays(4));
        var alive = Post(_aria, "Kept alive", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromSeconds(1));

        var gone = _service.Expire(_board.Id);

        Assert.Equal(new[] { old.Id, oldReply.Id }.Order(), gone.Order());
        Assert.Equal([alive.Id], _service.GetMessages(_board.Id).Select(message => message.Id));
    }

    [Fact]
    public void Expire_AReplyKeepsItsThreadAlive()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromDays(6));
        Post(_bruno, "Re: Horse", ["x"], thread.Id);
        _clock.Advance(TimeSpan.FromDays(6));

        Assert.Empty(_service.Expire(_board.Id));
        Assert.Equal(2, _service.Messages.Count);
    }

    [Fact]
    public void Expire_ExactlyAtTheDays_KeepsTheThread()
    {
        Post(_aria, "Horse", ["x"]);
        _clock.Advance(TimeSpan.FromDays(7));

        Assert.Empty(_service.Expire(_board.Id));
    }

    [Fact]
    public void Expire_WithZeroDays_KeepsEverything()
    {
        _config.ExpireDays = 0;
        Post(_aria, "Horse", ["x"]);
        _clock.Advance(TimeSpan.FromDays(5000));

        Assert.Empty(_service.Expire(_board.Id));
        Assert.Single(_service.Messages);
    }

    [Fact]
    public void AFullBoard_LetsGoTheThreadWithTheOldestLastReply_Whole()
    {
        _config.MaxMessages = 3;
        var first = Post(_aria, "First", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromMinutes(5));
        var second = Post(_bruno, "Second", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromMinutes(5));
        // The first thread is the older one, but its reply makes the second the one left longest alone.
        var reply = Post(_staff, "Re: First", ["x"], first.Id).Message!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = Post(_aria, "Third", ["x"]);

        Assert.Equal(BulletinPostResultType.Ok, result.Type);
        Assert.Equal([second.Id], result.Dropped);
        Assert.Equal([first.Id, reply.Id, result.Message!.Id], _service.GetMessages(_board.Id).Select(message => message.Id));
    }

    // The thread a reply was just added to is not the one that goes: its oldest replies do.
    [Fact]
    public void AFullBoardWithOneThread_KeepsTheThreadAndTheNewReply_AndLetsGoItsOldestReply()
    {
        _config.MaxMessages = 3;
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        _clock.Advance(TimeSpan.FromMinutes(5));
        var oldest = Post(_bruno, "Re: one", ["x"], thread.Id).Message!;
        _clock.Advance(TimeSpan.FromMinutes(5));
        var middle = Post(_staff, "Re: two", ["x"], thread.Id).Message!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = Post(_bruno, "Re: three", ["x"], thread.Id);

        Assert.Equal([oldest.Id], result.Dropped);
        Assert.Equal([thread.Id, middle.Id, result.Message!.Id], _service.GetMessages(_board.Id).Select(message => message.Id));
    }

    [Fact]
    public void TheSizeOfABoard_IsItsOwn()
    {
        _config.MaxMessages = 1;
        Post(_aria, "Here", ["x"]);

        var elsewhere = _service.Post(_other, _bruno, AccountType.Regular, Serial.Zero, "There", ["x"]);

        Assert.Empty(elsewhere.Dropped);
        Assert.Equal(2, _service.Messages.Count);
    }

    [Fact]
    public void Sweep_ExpiresOnEveryBoard_AndDropsTheMessagesOfABoardThatIsGone()
    {
        var here = Post(_aria, "Here", ["x"]).Message!;
        var there = _service.Post(_other, _bruno, AccountType.Regular, Serial.Zero, "There", ["x"]).Message!;
        _items.Remove([_other.Id]);

        _service.Sweep();

        Assert.Equal([here.Id], _service.Messages.Select(message => message.Id));
        Assert.Contains(there.Id, _service.Capture());

        _clock.Advance(TimeSpan.FromDays(8));
        _service.Sweep();

        Assert.Empty(_service.Messages);
    }

    [Fact]
    public async Task Start_RegistersASweepEveryHour_AndStopUnregistersIt()
    {
        await _service.StartAsync();

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((BulletinBoardService.TimerName, TimeSpan.FromHours(1), true), (timer.Name, timer.Interval, timer.Repeat));

        await _service.StopAsync();

        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public async Task Start_ReadsTheMessagesBack()
    {
        var thread = Post(_aria, "Horse", ["x"]).Message!;
        var reply = Post(_bruno, "Re: Horse", ["x"], thread.Id).Message!;
        _table.Upserted.AddRange(_service.Messages.Select(message => message.Snapshot()));

        var again = Create();
        await again.StartAsync();

        Assert.Equal([thread.Id, reply.Id], again.GetMessages(_board.Id).Select(message => message.Id));
        Assert.Equal("Horse", again.GetMessage(thread.Id)?.Subject);
    }

    // A row deleted by hand, or a save cut short: a reply without its thread could never be listed in its place.
    [Fact]
    public async Task Start_DropsAReplyWhoseThreadIsGone()
    {
        _table.Upserted.Add(
            new BulletinMessageEntity { Id = new Serial(0x40002001), BoardId = _board.Id, ThreadId = new Serial(0x40002000), Subject = "Re: gone", Body = "x" }
        );
        _table.Upserted.Add(new BulletinMessageEntity { Id = new Serial(0x40002002), BoardId = _board.Id, Subject = "Kept", Body = "x" });

        var again = Create();
        await again.StartAsync();

        Assert.Equal([new Serial(0x40002002)], again.Messages.Select(message => message.Id));
        Assert.Equal([new Serial(0x40002001)], again.Capture());
    }

    [Fact]
    public void Capture_HasWhatWasRemoved_UntilItIsCommitted()
    {
        var message = Post(_aria, "Horse", ["x"]).Message!;
        _service.Remove(message.Id);

        var captured = _service.Capture();

        Assert.Equal([message.Id], captured);

        _service.Committed(captured);

        Assert.Empty(_service.Capture());
    }

    private BulletinPostResult Post(MobileEntity poster, string subject, string[] lines, Serial replyTo = default)
    {
        return _service.Post(_board, poster, AccountType.Regular, replyTo, subject, lines);
    }

    private ItemEntity Board(uint serial)
    {
        var board = new ItemEntity { Id = new Serial(serial), TemplateId = "bulletin_board", ItemId = 0x1E5E, Amount = 1 };
        board.PlaceOnGround(MapType.Trammel, new Point3D(1492, 1607, 20));
        _items.Add([board]);

        return board;
    }

    private void Wear(MobileEntity mobile, int itemId, ushort hue, LayerType layer)
    {
        var item = new ItemEntity { Id = new Serial(0x40000100 + (uint)layer), TemplateId = "chair", ItemId = itemId, Amount = 1, Hue = new Hue(hue) };
        item.Equip(mobile.Id, layer);
        _items.Add([item]);
    }

    // The pool hands out a serial for each post, as the real one does.
    private BulletinBoardService Create(StubItemSerialPool? serials = null)
    {
        if (serials is null)
        {
            serials = _serials;

            for (var index = 0; index < 64; index++)
            {
                serials.Serials.Enqueue(new Serial(FirstSerial + (uint)index));
            }
        }

        return new BulletinBoardService(_table, _items, _templates, serials, _timers, _config, _clock, new StubPacketSendService());
    }
}
