using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class JailServiceTests : IAsyncLifetime
{
    private const long Day = 86_400_000;

    private static readonly Point3D Cell1 = new(5276, 1164, 0);
    private static readonly Point3D Cell2 = new(5286, 1164, 0);

    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingDataAccess<JailSentenceEntity> _data = new();
    private readonly RecordingDataAccess<MobileEntity> _characters = new();
    private readonly RecordingDataAccess<AccountEntity> _accounts = new();
    private readonly JailConfig _config = new();
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero) };
    private readonly ItemsConfig _itemsConfig = new() { GoldTemplate = "gold", BackpackTemplate = "backpack" };
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                 .Item(0x0EED, TileFlagType.Generic, 0)
                                                 .Item(0x0E75, TileFlagType.Container, 0)
                                                 .Item(0x0E7C, TileFlagType.Container, 0)
                                                 .Item(0x14ED, TileFlagType.Generic, 0);
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
            new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
            new ItemTemplate { Id = JailService.NoteTemplate, ItemId = new Serial(0x14ED), Name = "a release note" }
        )
    );

    private uint _nextItem = 0x40000001;
    private readonly JailFile _file = new()
    {
        Map = MapType.Felucca,
        Release = new Point3D(1444, 1697, 10),
        Cell = [new JailCell { Number = 1, Location = Cell1 }, new JailCell { Number = 2, Location = Cell2 }]
    };

    private BroadcastFixture _fixture = null!;
    private readonly CapturingLogSink _log = new();
    private int _read;

    private MobileEntity _aria = null!;
    private MobileEntity _bruno = null!;
    private MobileEntity _staff = null!;
    private MobileEntity _orc = null!;
    private JailService _jail = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _aria = await AddPlayerAsync(2, "Aria", AccountType.Regular);
        _bruno = await AddPlayerAsync(3, "Bruno", AccountType.Regular);
        _staff = await AddPlayerAsync(4, "Giachi", AccountType.GameMaster);
        _aria.Location = new Point3D(1600, 1600, 5);
        _orc = new MobileEntity { Id = new Serial(900), Name = "an orc", Map = MapType.Trammel, Location = new Point3D(1700, 1700, 0) };
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_orc));
        _jail = await CreateAsync(_file);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Jail_APlayer_TeleportsItToTheCellAndKeepsWhereItWas()
    {
        Assert.Equal(JailResultType.Ok, _jail.Jail(_aria, 2, 3, _staff));

        Assert.Equal((_aria, MapType.Felucca, Cell2), Assert.Single(_teleports.Teleports));
        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((_aria.Id, "Aria", true, 2, 3, "Giachi", false), (sentence.Id, sentence.Name, sentence.IsPlayer, sentence.Cell, sentence.Days, sentence.JailedBy, sentence.Pardoned));
        Assert.Equal(
            (MapType.Trammel, 1600, 1600, 5),
            (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ)
        );
        var now = _clock.Now.ToUnixTimeMilliseconds();
        Assert.Equal((now, now + 3 * Day), (sentence.JailedAt, sentence.ReleaseAt));
        Assert.Same(sentence, _jail.GetSentence(_aria.Id));
        Assert.Same(sentence, _jail.GetOccupant(2));
        Assert.Null(_jail.GetOccupant(1));
    }

    [Fact]
    public void Jail_AnNpc_Works_AndIsNotAPlayerSentence()
    {
        Assert.Equal(JailResultType.Ok, _jail.Jail(_orc, 1, 1, _staff));

        Assert.False(Assert.Single(_jail.Sentences).IsPlayer);
    }

    [Fact]
    public void Jail_TellsThePlayerTheDays()
    {
        _jail.Jail(_aria, 1, 3, _staff);

        Assert.Equal((_aria, "You have been jailed for 3 days."), Assert.Single(_speech.Told));
    }

    [Fact]
    public void Jail_WithAReason_KeepsIt_AndTellsThePlayerWhy()
    {
        _jail.Jail(_aria, 1, 3, _staff, "  Stole a horse  ");

        Assert.Equal("Stole a horse", Assert.Single(_jail.Sentences).Reason);
        Assert.Equal((_aria, "You have been jailed for 3 days: Stole a horse"), Assert.Single(_speech.Told));
        Assert.Equal(["Aria (0x00000002) is jailed in cell 1 for 3 days by Giachi: Stole a horse"], Logged());
    }

    [Theory,
     // Line breaks and tabs become spaces: the reason is one line, on the note and in the log.
     InlineData("Stole\na\thorse", "Stole a horse"),
     InlineData("   ", ""),
     InlineData(null, "")]
    public void Jail_CleansTheReason(string? typed, string kept)
    {
        _jail.Jail(_aria, 1, 3, _staff, typed);

        Assert.Equal(kept, Assert.Single(_jail.Sentences).Reason);
    }

    [Fact]
    public void Jail_CutsAReasonThatIsTooLong()
    {
        _jail.Jail(_aria, 1, 3, _staff, new string('x', 500));

        Assert.Equal(JailService.MaxReasonLength, Assert.Single(_jail.Sentences).Reason.Length);
    }

    [Fact]
    public void Jail_CutsALongReason_WithoutSplittingACharacterInTwo()
    {
        // The hundredth place falls in the middle of a character of two code units.
        _jail.Jail(_aria, 1, 3, _staff, new string('x', 99) + "\U0001F600" + "tail");

        var reason = Assert.Single(_jail.Sentences).Reason;
        Assert.Equal(new string('x', 99), reason);
    }

    [Fact]
    public void Jail_TakesTheAngleBracketsOutOfAReason_SoTheNoteShowsNoMarkup()
    {
        _jail.Jail(_aria, 1, 3, _staff, "Stole <basefont color=red>a horse</basefont>");

        Assert.Equal("Stole basefont color=red a horse /basefont", Assert.Single(_jail.Sentences).Reason);
    }

    [Fact]
    public async Task Start_ASentenceSavedBeforeTheReasonExisted_HasAnEmptyOne()
    {
        _data.Upserted.Add(
            new()
            {
                Id = new Serial(77), Name = "Old", Cell = 1, Days = 3, JailedBy = "Giachi", Reason = null!,
                ReleaseAt = _clock.Now.ToUnixTimeMilliseconds() + Day
            }
        );

        var jail = await CreateAsync(_file);

        Assert.Equal("", Assert.Single(jail.Sentences).Reason);
    }

    [Fact]
    public void Jail_AgainInAnotherCell_TakesTheNewReason_EvenNone()
    {
        _jail.Jail(_aria, 2, 3, _staff, "Stole a horse");

        _jail.Jail(_aria, 1, 5, _staff);

        Assert.Equal("", Assert.Single(_jail.Sentences).Reason);
    }

    [Fact]
    public void Check_WritesTheReasonOnTheNote()
    {
        var backpack = Backpack(_aria);
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        _jail.Jail(_aria, 2, 3, _staff, "Stole a horse");
        _clock.Advance(TimeSpan.FromDays(3));

        _jail.Check();

        var note = Assert.Single(_items.GetContents(backpack.Id), item => item.TemplateId == JailService.NoteTemplate);
        Assert.Equal(
            "Aria served 3 days in cell 2, from 2026-10-04 to 2026-10-07, and paid a fine of 0 gold. Jailed by Giachi. Reason: Stole a horse",
            note.Props![JailService.NoteTextProp]
        );
    }

    [Fact]
    public void Jail_AnOccupiedCell_IsRefused()
    {
        _jail.Jail(_aria, 1, 3, _staff);

        Assert.Equal(JailResultType.CellOccupied, _jail.Jail(_bruno, 1, 1, _staff));

        Assert.Single(_jail.Sentences);
        Assert.Single(_teleports.Teleports);
    }

    [Fact]
    public void Jail_ACellWhoseSentenceIsOver_IsFree()
    {
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        Assert.Null(_jail.GetOccupant(1));
        Assert.Equal(JailResultType.Ok, _jail.Jail(_bruno, 1, 1, _staff));
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(31)]
    public void Jail_DaysOutOfRange_AreRefused(int days)
    {
        Assert.Equal(JailResultType.BadDays, _jail.Jail(_aria, 1, days, _staff));

        Assert.Empty(_jail.Sentences);
        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public void Jail_TheLongestSentence_IsAccepted()
    {
        Assert.Equal(30, _jail.MaxDays);
        Assert.Equal(JailResultType.Ok, _jail.Jail(_aria, 1, 30, _staff));
    }

    [Fact]
    public void Jail_AnUnknownCell_IsRefused()
    {
        Assert.Equal(JailResultType.NoSuchCell, _jail.Jail(_aria, 3, 1, _staff));
    }

    [Fact]
    public void Jail_Oneself_IsRefused()
    {
        Assert.Equal(JailResultType.Refused, _jail.Jail(_staff, 1, 1, _staff));
    }

    [Fact]
    public async Task Jail_AStaffMemberOfTheSameRank_IsRefused()
    {
        var other = await AddPlayerAsync(5, "Other", AccountType.GameMaster);

        Assert.Equal(JailResultType.Refused, _jail.Jail(other, 1, 1, _staff));
        Assert.Empty(_jail.Sentences);
    }

    [Fact]
    public void Jail_AMobileNotInTheWorld_IsRefused()
    {
        var ghost = new MobileEntity { Id = new Serial(77), Name = "Ghost" };

        Assert.Equal(JailResultType.NotInWorld, _jail.Jail(ghost, 1, 1, _staff));
    }

    [Fact]
    public async Task Jail_WithoutTheFile_IsDisabled()
    {
        var jail = await CreateAsync(null);

        Assert.False(jail.IsEnabled);
        Assert.Empty(jail.Cells);
        Assert.Equal(JailResultType.Disabled, jail.Jail(_aria, 1, 1, _staff));
    }

    [Fact]
    public void Jail_WhenTheJailMapIsNotLoaded_IsRefused_AndKeepsNothing()
    {
        _teleports.Result = false;

        Assert.Equal(JailResultType.MapNotLoaded, _jail.Jail(_aria, 1, 1, _staff));

        Assert.Empty(_jail.Sentences);
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void Jail_APrisonerAgain_MovesItAndKeepsTheFirstReturnPlace()
    {
        _jail.Jail(_aria, 1, 1, _staff);
        _aria.Map = MapType.Felucca;
        _aria.Location = Cell1;
        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(JailResultType.Ok, _jail.Jail(_aria, 2, 5, _staff));

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((2, 5), (sentence.Cell, sentence.Days));
        Assert.Equal((MapType.Trammel, 1600, 1600, 5), (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ));
        Assert.Equal(_clock.Now.ToUnixTimeMilliseconds() + 5 * Day, sentence.ReleaseAt);
        Assert.Null(_jail.GetOccupant(1));
    }

    [Fact]
    public void Jail_APrisonerAgainInItsOwnCell_IsNotOccupiedByItself()
    {
        _jail.Jail(_aria, 1, 1, _staff);

        Assert.Equal(JailResultType.Ok, _jail.Jail(_aria, 1, 2, _staff));
        Assert.Equal(2, Assert.Single(_jail.Sentences).Days);
    }

    [Fact]
    public async Task Start_LoadsTheSentencesOfTheTable_SoTheirCellsStayOccupied()
    {
        _data.Upserted.Add(
            new JailSentenceEntity
            {
                Id = _bruno.Id, Name = "Bruno", IsPlayer = true, Cell = 1, Days = 2,
                ReleaseAt = _clock.Now.ToUnixTimeMilliseconds() + 2 * Day
            }
        );
        var jail = await CreateAsync(_file);

        Assert.Equal("Bruno", jail.GetOccupant(1)?.Name);
        Assert.Equal(JailResultType.CellOccupied, jail.Jail(_aria, 1, 1, _staff));
    }

    [Fact]
    public async Task Start_RegistersACheckEveryTenSeconds_AndStopUnregistersIt()
    {
        var timer = _timers.Timers[^1];

        Assert.Equal((JailService.TimerName, TimeSpan.FromSeconds(10), true), (timer.Name, timer.Interval, timer.Repeat));

        await _jail.StopAsync();

        Assert.Contains(timer.Id, _timers.Unregistered);
    }

    [Fact]
    public void Check_ASentenceStillRunning_DoesNothing()
    {
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromHours(23));

        _jail.Check();

        Assert.Single(_jail.Sentences);
        Assert.Single(_teleports.Teleports);
    }

    [Fact]
    public void Check_ASentenceThatIsOver_SendsThePrisonerBackWhereItWas_AndEndsTheSentence()
    {
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Equal((_aria, MapType.Trammel, new Point3D(1600, 1600, 5)), _teleports.Teleports[^1]);
        Assert.Empty(_jail.Sentences);
        Assert.Null(_jail.GetSentence(_aria.Id));
        Assert.Equal([_aria.Id], _jail.Capture());

        _jail.Committed([_aria.Id]);

        Assert.Empty(_jail.Capture());
    }

    [Fact]
    public void Check_TakesTheFineFromTheBackpack_AndSaysSo()
    {
        var backpack = Backpack(_aria);
        var gold = Gold(backpack, 800);
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Equal(300, gold.Amount);
        Assert.Equal((_aria, "You have served your sentence. A fine of 500 gold was taken."), _speech.Told[^1]);
    }

    [Fact]
    public void Check_TakesWhatTheBackpackLacksFromTheBank()
    {
        var carried = Gold(Backpack(_aria), 200);
        var banked = Gold(Bank(_aria), 1000);
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.False(_items.TryGet(carried.Id, out _));
        Assert.Equal(700, banked.Amount);
    }

    [Fact]
    public void Check_GoldInSeveralPilesAndABag_TakesExactlyTheFine_BackpackFirst()
    {
        var backpack = Backpack(_aria);
        var bag = Item("backpack", 0x0E75, 1);
        bag.PutInContainer(backpack.Id, new Point2D(10, 10));
        _items.Add([bag]);
        var first = Gold(backpack, 100);
        var second = Gold(backpack, 150);
        var inBag = Gold(bag, 120);
        var banked = Gold(Bank(_aria), 1000);
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.False(_items.TryGet(first.Id, out _));
        Assert.False(_items.TryGet(second.Id, out _));
        Assert.False(_items.TryGet(inBag.Id, out _));
        Assert.Equal(870, banked.Amount);
    }

    [Fact]
    public void Check_NotEnoughGold_TakesWhatThereIsAndStillReleases()
    {
        var gold = Gold(Backpack(_aria), 120);
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.False(_items.TryGet(gold.Id, out _));
        Assert.Empty(_jail.Sentences);
        Assert.Equal("You have served your sentence. A fine of 120 gold was taken.", _speech.Told[^1].Text);
    }

    [Fact]
    public void Check_AFineOfZero_TakesNothing()
    {
        _config.FineGold = 0;
        var gold = Gold(Backpack(_aria), 800);
        _jail.Jail(_aria, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Equal(800, gold.Amount);
        Assert.Equal("You have served your sentence.", _speech.Told[^1].Text);
    }

    [Fact]
    public void Check_AnNpcWithoutGoldOrBackpack_IsReleasedWithNoFineAndNoNote()
    {
        _jail.Jail(_orc, 1, 1, _staff);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Equal((_orc, MapType.Trammel, new Point3D(1700, 1700, 0)), _teleports.Teleports[^1]);
        Assert.Empty(_jail.Sentences);
        Assert.Empty(_items.GetOwnedBy(_orc.Id));
    }

    [Fact]
    public void Check_GivesTheNoteWithTheTextTheDaysTheCellAndTheFine()
    {
        var backpack = Backpack(_aria);
        Gold(backpack, 800);
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        _jail.Jail(_aria, 2, 3, _staff);
        _clock.Advance(TimeSpan.FromDays(3));

        _jail.Check();

        var note = Assert.Single(_items.GetContents(backpack.Id), item => item.TemplateId == JailService.NoteTemplate);
        Assert.Equal(
            "Aria served 3 days in cell 2, from 2026-10-04 to 2026-10-07, and paid a fine of 500 gold. Jailed by Giachi.",
            note.Props![JailService.NoteTextProp]
        );
        Assert.Equal((2, 3, 500), (Convert.ToInt32(note.Props["jail.cell"]), Convert.ToInt32(note.Props["jail.days"]), Convert.ToInt32(note.Props["jail.fine"])));
    }

    [Fact]
    public async Task Check_AnOfflinePlayer_StaysUntilItIsBack_ThenIsReleasedOnce()
    {
        var gold = Gold(Backpack(_aria), 2000);
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        _serials.Serials.Enqueue(new Serial(0x40000F01));
        _jail.Jail(_aria, 1, 1, _staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_aria.Id));
        _clock.Advance(TimeSpan.FromDays(2));

        _jail.Check();

        Assert.Single(_jail.Sentences);
        Assert.Null(_jail.GetOccupant(1));
        Assert.Equal(2000, gold.Amount);

        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_aria));
        _jail.Check();
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal(1500, gold.Amount);
        Assert.Single(_items.GetOwnedBy(_aria.Id), item => item.TemplateId == JailService.NoteTemplate);
    }

    [Fact]
    public async Task Check_ARemovedNpc_DropsItsSentence()
    {
        _jail.Jail(_orc, 1, 1, _staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_orc.Id));
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal([_orc.Id], _jail.Capture());
        Assert.Single(_teleports.Teleports);
    }

    [Fact]
    public void Check_WhenTheOldMapIsGone_UsesTheReleaseSpot()
    {
        _jail.Jail(_aria, 1, 1, _staff);
        _teleports.RefusedMaps.Add(MapType.Trammel);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Equal((_aria, MapType.Felucca, new Point3D(1444, 1697, 10)), _teleports.Teleports[^1]);
        Assert.Empty(_jail.Sentences);
    }

    [Fact]
    public void Check_OnePrisonerFailing_DoesNotStopTheOthers_AndIsNotFinedTwice()
    {
        var gold = Gold(Backpack(_aria), 2000);
        _jail.Jail(_aria, 1, 1, _staff);
        _jail.Jail(_bruno, 2, 1, _staff);
        _teleports.ThrowFor = _aria;
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Contains(_teleports.Teleports, teleport => teleport.Mobile == _bruno && teleport.Map == MapType.Trammel);
        Assert.Equal(1500, gold.Amount);
    }

    // A pile on the cursor cannot be taken: lifting the gold must not be a way around the fine.
    [Fact]
    public async Task Check_APrisonerHoldingSomethingOnItsCursor_WaitsUntilItIsDropped_ThenPaysTheWholeFine()
    {
        var gold = Gold(Backpack(_aria), 800);
        Assert.True(_fixture.Sessions.TryGetByCharacterId(_aria.Id, out var session));
        _jail.Jail(_aria, 1, 1, _staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(ItemSessionKeys.Held, new HeldItem(gold.Id)));
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Single(_jail.Sentences);
        Assert.Equal(800, gold.Amount);
        Assert.Single(_teleports.Teleports);

        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(ItemSessionKeys.Held, null));
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal(300, gold.Amount);
    }

    [Fact]
    public async Task Pardon_APrisonerHoldingSomethingOnItsCursor_ReleasesItAtOnce()
    {
        var gold = Gold(Backpack(_aria), 800);
        Assert.True(_fixture.Sessions.TryGetByCharacterId(_aria.Id, out var session));
        _jail.Jail(_aria, 1, 1, _staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(ItemSessionKeys.Held, new HeldItem(gold.Id)));

        Assert.True(_jail.Pardon(_aria.Id));

        Assert.Empty(_jail.Sentences);
        Assert.Equal(800, gold.Amount);
    }

    // The client is still being sent its login: a teleport now would reach it before it knows where it stands.
    [Fact]
    public void Check_APlayerWhoseLoginIsNotOverYet_WaitsForTheNextCheck()
    {
        var gold = Gold(Backpack(_aria), 800);
        _jail.Jail(_aria, 1, 1, _staff);
        _view.NotEntered.Add(_aria.Id);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Single(_jail.Sentences);
        Assert.Equal(800, gold.Amount);
        Assert.Single(_teleports.Teleports);

        _view.NotEntered.Clear();
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal(300, gold.Amount);
    }

    [Fact]
    public void Check_AnNpc_NeedsNoLogin()
    {
        _jail.Jail(_orc, 1, 1, _staff);
        _view.NotEntered.Add(_orc.Id);
        _clock.Advance(TimeSpan.FromDays(1));

        _jail.Check();

        Assert.Empty(_jail.Sentences);
    }

    [Fact]
    public async Task Start_ASentenceThatEndedWhileTheServerWasDown_IsReleasedAtTheFirstCheck()
    {
        _data.Upserted.Add(
            new JailSentenceEntity
            {
                Id = _bruno.Id, Name = "Bruno", IsPlayer = true, Cell = 1, Days = 2, JailedBy = "Giachi",
                JailedAt = _clock.Now.ToUnixTimeMilliseconds() - 3 * Day,
                ReleaseAt = _clock.Now.ToUnixTimeMilliseconds() - Day,
                ReturnMap = MapType.Trammel, ReturnX = 1500, ReturnY = 1500, ReturnZ = 0
            }
        );
        var jail = await CreateAsync(_file);

        jail.Check();

        Assert.Equal((_bruno, MapType.Trammel, new Point3D(1500, 1500, 0)), Assert.Single(_teleports.Teleports));
        Assert.Empty(jail.Sentences);
    }

    [Fact]
    public void TheConsole_IsToldWhoIsJailed_WhoIsReleasedWithItsFine_AndWhoIsReleasedEarly()
    {
        var backpack = Backpack(_aria);
        Gold(backpack, 800);
        _serials.Serials.Enqueue(new Serial(0x40000F00));

        _jail.Jail(_aria, 2, 3, _staff);
        _jail.Jail(_orc, 1, 1, _staff);
        Assert.Equal(
            ["Aria (0x00000002) is jailed in cell 2 for 3 days by Giachi", $"an orc ({_orc.Id}) is jailed in cell 1 for 1 days by Giachi"],
            Logged()
        );

        Logged();
        Assert.True(_jail.Pardon(_orc.Id));
        Assert.Equal([$"an orc ({_orc.Id}) is released early from cell 1"], Logged());

        Logged();
        _clock.Advance(TimeSpan.FromDays(3));
        _jail.Check();
        Assert.Equal(["Aria (0x00000002) is released from cell 2 after 3 days, with a fine of 500 gold"], Logged());
        Assert.All(_log.Events, entry => Assert.Equal(Serilog.Events.LogEventLevel.Information, entry.Level));
    }

    [Fact]
    public void TheConsole_IsToldWhenACharacterIsMovedToAnotherCell()
    {
        _jail.Jail(_aria, 2, 3, _staff);
        Logged();

        Assert.Equal(JailResultType.Ok, _jail.Jail(_aria, 1, 5, _staff));

        Assert.Equal(["Aria (0x00000002) is jailed in cell 1 for 5 days by Giachi"], Logged());
    }

    [Fact]
    public void Pardon_AnOnlinePrisoner_GoesBackAtOnceWithNoFineAndNoNote()
    {
        var backpack = Backpack(_aria);
        var gold = Gold(backpack, 800);
        _jail.Jail(_aria, 1, 3, _staff);

        Assert.True(_jail.Pardon(_aria.Id));

        Assert.Equal((_aria, MapType.Trammel, new Point3D(1600, 1600, 5)), _teleports.Teleports[^1]);
        Assert.Empty(_jail.Sentences);
        Assert.Equal(800, gold.Amount);
        Assert.Single(_items.GetContents(backpack.Id));
        Assert.Equal("You have been released from jail.", _speech.Told[^1].Text);
    }

    [Fact]
    public async Task Pardon_AnOfflinePlayer_FreesTheCellNow_AndReleasesItAtLoginWithNoFine()
    {
        var gold = Gold(Backpack(_aria), 800);
        _jail.Jail(_aria, 1, 3, _staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_aria.Id));

        Assert.True(_jail.Pardon(_aria.Id));

        Assert.Null(_jail.GetOccupant(1));
        Assert.Single(_jail.Sentences);

        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_aria));
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal(800, gold.Amount);
        Assert.Equal(new Point3D(1600, 1600, 5), _teleports.Teleports[^1].Location);
    }

    [Fact]
    public async Task Find_GivesThePlayersOfThatName_WithTheirAccountAndRank()
    {
        Character(200, "Pippo", "mario", AccountType.Regular);
        Character(201, "Gino", "luigi", AccountType.Regular);

        var found = Assert.Single(await _jail.FindAsync("Pippo"));

        Assert.Equal(
            (new Serial(200), "Pippo", "mario", AccountType.Regular),
            (found.Id, found.Name, found.Account, found.AccountType)
        );
    }

    [Fact]
    public async Task Find_IgnoresCaseAndTheSpacesAround()
    {
        Character(200, "Pippo", "mario", AccountType.Regular);

        Assert.Equal(new Serial(200), Assert.Single(await _jail.FindAsync("  pIPPO ")).Id);
    }

    [Fact]
    public async Task Find_LeavesOutNpcsAndCharactersPendingDeletion()
    {
        _characters.Upserted.Add(new MobileEntity { Id = new Serial(300), Name = "Pippo" });
        Character(200, "Pippo", "mario", AccountType.Regular).DeletionRequestedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Empty(await _jail.FindAsync("Pippo"));
    }

    [Fact]
    public async Task Find_SeveralOfTheSameName_GivesThemAllInSerialOrder()
    {
        Character(210, "Pippo", "luigi", AccountType.GameMaster);
        Character(200, "Pippo", "mario", AccountType.Regular);

        var found = await _jail.FindAsync("pippo");

        Assert.Equal(
            [(new Serial(200), "mario", AccountType.Regular), (new Serial(210), "luigi", AccountType.GameMaster)],
            found.Select(candidate => (candidate.Id, candidate.Account, candidate.AccountType))
        );
    }

    [Theory, InlineData(""), InlineData("   ")]
    public async Task Find_AnEmptyName_FindsNobody(string name)
    {
        Character(200, "", "mario", AccountType.Regular);

        Assert.Empty(await _jail.FindAsync(name));
    }

    [Fact]
    public async Task JailOffline_AFoundPlayer_KeepsASentenceThatWaits_AndMovesNobody()
    {
        var aria = await OfflineAsync(_aria);

        Assert.Equal(JailResultType.Pending, _jail.JailOffline(aria, 2, 3, _staff, " Stole a horse "));

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal(
            (aria, "Aria", true, 2, 3, "Giachi", "Stole a horse", true, false),
            (sentence.Id, sentence.Name, sentence.IsPlayer, sentence.Cell, sentence.Days, sentence.JailedBy, sentence.Reason, sentence.Pending, sentence.Pardoned)
        );
        // Its days have not started.
        Assert.Equal((0L, 0L), (sentence.JailedAt, sentence.ReleaseAt));
        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public async Task JailOffline_ReservesTheCell()
    {
        var aria = await OfflineAsync(_aria);

        _jail.JailOffline(aria, 2, 3, _staff);

        Assert.Equal(aria, _jail.GetOccupant(2)?.Id);
        Assert.Equal(JailResultType.CellOccupied, _jail.Jail(_bruno, 2, 1, _staff));

        // However long it waits.
        _clock.Advance(TimeSpan.FromDays(400));
        Assert.Equal(aria, _jail.GetOccupant(2)?.Id);
    }

    [Fact]
    public async Task JailOffline_ASerialTheJailDidNotFind_IsRefused()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_aria.Id));

        Assert.Equal(JailResultType.NotInWorld, _jail.JailOffline(_aria.Id, 2, 3, _staff));
        Assert.Empty(_jail.Sentences);
    }

    [Theory, InlineData(AccountType.GameMaster), InlineData(AccountType.Administrator)]
    public async Task JailOffline_AnAccountOfTheSameRankOrAbove_IsRefused(AccountType rank)
    {
        var aria = await OfflineAsync(_aria, rank);

        Assert.Equal(JailResultType.Refused, _jail.JailOffline(aria, 2, 3, _staff));
        Assert.Empty(_jail.Sentences);
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(31)]
    public async Task JailOffline_DaysOutOfRange_AreRefused(int days)
    {
        var aria = await OfflineAsync(_aria);

        Assert.Equal(JailResultType.BadDays, _jail.JailOffline(aria, 2, days, _staff));
        Assert.Empty(_jail.Sentences);
    }

    [Fact]
    public async Task JailOffline_AnUnknownCell_IsRefused()
    {
        var aria = await OfflineAsync(_aria);

        Assert.Equal(JailResultType.NoSuchCell, _jail.JailOffline(aria, 9, 3, _staff));
    }

    [Fact]
    public async Task JailOffline_WithoutTheFile_IsDisabled()
    {
        var jail = await CreateAsync(null);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_aria.Id));
        Character(_aria.Id.Value, "Aria", "mario", AccountType.Regular);
        await jail.FindAsync("Aria");

        Assert.Equal(JailResultType.Disabled, jail.JailOffline(_aria.Id, 1, 3, _staff));
    }

    [Fact]
    public async Task JailOffline_AnOccupiedCell_IsRefused()
    {
        var aria = await OfflineAsync(_aria);
        _jail.Jail(_bruno, 2, 1, _staff);

        Assert.Equal(JailResultType.CellOccupied, _jail.JailOffline(aria, 2, 3, _staff));
        Assert.Single(_jail.Sentences);
    }

    [Fact]
    public async Task JailOffline_AgainInAnotherCell_MovesTheReservation()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 1, 3, _staff, "Stole a horse");

        Assert.Equal(JailResultType.Pending, _jail.JailOffline(aria, 2, 5, _staff));

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((2, 5, "", true), (sentence.Cell, sentence.Days, sentence.Reason, sentence.Pending));
        Assert.Null(_jail.GetOccupant(1));
    }

    // Found offline by .jail, back in the world before the game master picked the cell.
    [Fact]
    public async Task JailOffline_APlayerWhoLoggedInMeanwhile_IsJailedAtOnce()
    {
        var aria = await OfflineAsync(_aria);
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_aria));

        Assert.Equal(JailResultType.Ok, _jail.JailOffline(aria, 2, 3, _staff));

        Assert.Equal((_aria, MapType.Felucca, Cell2), Assert.Single(_teleports.Teleports));
        Assert.False(Assert.Single(_jail.Sentences).Pending);
    }

    [Fact]
    public async Task JailOffline_ARunningSentenceOfAnOfflinePlayer_WaitsAgain_AndKeepsItsReturnPlace()
    {
        _jail.Jail(_aria, 1, 3, _staff);
        var aria = await OfflineAsync(_aria);

        Assert.Equal(JailResultType.Pending, _jail.JailOffline(aria, 2, 5, _staff));

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((2, 5, true), (sentence.Cell, sentence.Days, sentence.Pending));
        Assert.Equal((MapType.Trammel, 1600, 1600, 5), (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ));
        Assert.Null(_jail.GetOccupant(1));
    }

    [Fact]
    public async Task TheConsole_IsToldWhoWillBeJailedAtItsLogin()
    {
        var aria = await OfflineAsync(_aria);
        Logged();

        _jail.JailOffline(aria, 2, 3, _staff, "Stole a horse");
        _jail.JailOffline(aria, 1, 1, _staff);

        Assert.Equal(
            [
                "Aria (0x00000002) will be jailed in cell 2 for 3 days at its next login, by Giachi: Stole a horse",
                "Aria (0x00000002) will be jailed in cell 1 for 1 days at its next login, by Giachi"
            ],
            Logged()
        );
    }

    [Fact]
    public async Task Check_AWaitingSentence_WhileItsPrisonerIsOffline_DoesNothing()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        _clock.Advance(TimeSpan.FromDays(10));

        _jail.Check();

        Assert.True(Assert.Single(_jail.Sentences).Pending);
        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_jail.Capture());
    }

    [Fact]
    public async Task Check_AWaitingSentence_AtTheLogin_TakesThePrisonerToItsCell_AndItsDaysStartThen()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff, "Stole a horse");
        _clock.Advance(TimeSpan.FromDays(5));
        await LoginAsync(_aria, MapType.Felucca, new Point3D(2000, 2100, 7));
        Logged();

        _jail.Check();

        Assert.Equal((_aria, MapType.Felucca, Cell2), Assert.Single(_teleports.Teleports));
        var sentence = Assert.Single(_jail.Sentences);
        var now = _clock.Now.ToUnixTimeMilliseconds();
        Assert.Equal((false, now, now + 3 * Day), (sentence.Pending, sentence.JailedAt, sentence.ReleaseAt));
        // It goes back where it logged in.
        Assert.Equal((MapType.Felucca, 2000, 2100, 7), (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ));
        Assert.Equal((_aria, "You have been jailed for 3 days: Stole a horse"), Assert.Single(_speech.Told));
        Assert.Equal(["Aria (0x00000002) is jailed in cell 2 for 3 days by Giachi: Stole a horse"], Logged());

        // Once: the next check finds a sentence that runs.
        _jail.Check();
        Assert.Single(_teleports.Teleports);
        Assert.Single(_speech.Told);
    }

    [Fact]
    public async Task Check_AWaitingSentence_WhoseLoginIsNotOverYet_WaitsForTheNextCheck()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        await LoginAsync(_aria, MapType.Trammel, new Point3D(1600, 1600, 5));
        _view.NotEntered.Add(aria);

        _jail.Check();

        Assert.True(Assert.Single(_jail.Sentences).Pending);
        Assert.Empty(_teleports.Teleports);

        _view.NotEntered.Clear();
        _jail.Check();

        Assert.False(Assert.Single(_jail.Sentences).Pending);
        Assert.Single(_teleports.Teleports);
    }

    [Fact]
    public async Task Check_AWaitingSentence_WhenTheJailMapIsNotLoaded_KeepsWaiting()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        await LoginAsync(_aria, MapType.Trammel, new Point3D(1600, 1600, 5));
        _teleports.Result = false;

        _jail.Check();

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((true, 0L, 0L), (sentence.Pending, sentence.JailedAt, sentence.ReleaseAt));
        Assert.Empty(_speech.Told);

        // The map is back: the same sentence starts.
        _teleports.Result = true;
        _jail.Check();

        Assert.False(sentence.Pending);
    }

    [Fact]
    public async Task Check_AWaitingSentence_WhoseCellIsGoneFromTheFile_KeepsWaiting_AndMovesNobody()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        await LoginAsync(_aria, MapType.Trammel, new Point3D(1600, 1600, 5));
        _file.Cell.RemoveAll(cell => cell.Number == 2);

        _jail.Check();

        Assert.True(Assert.Single(_jail.Sentences).Pending);
        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public async Task Check_ASentenceStartedAtTheLogin_EndsAfterItsDays_WithTheFineAndTheNote()
    {
        var gold = Gold(Backpack(_aria), 800);
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        await LoginAsync(_aria, MapType.Trammel, new Point3D(1600, 1600, 5));
        _jail.Check();

        _clock.Advance(TimeSpan.FromDays(3) - TimeSpan.FromSeconds(1));
        _jail.Check();
        Assert.Single(_jail.Sentences);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal((_aria, MapType.Trammel, new Point3D(1600, 1600, 5)), _teleports.Teleports[^1]);
        Assert.Equal(300, gold.Amount);
        Assert.Single(_items.GetOwnedBy(_aria.Id), item => item.TemplateId == JailService.NoteTemplate);
    }

    // Back in the world with a sentence that waits, and jailed by hand before the check got to it.
    [Fact]
    public async Task Jail_APlayerWhoseSentenceWaits_StartsItNow_AndKeepsWhereItWas()
    {
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        await LoginAsync(_aria, MapType.Felucca, new Point3D(2000, 2100, 7));

        Assert.Equal(JailResultType.Ok, _jail.Jail(_aria, 1, 4, _staff));

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((false, 1, 4), (sentence.Pending, sentence.Cell, sentence.Days));
        Assert.Equal((MapType.Felucca, 2000, 2100, 7), (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ));

        // The check has nothing left to start.
        _jail.Check();
        Assert.Single(_teleports.Teleports);
    }

    [Fact]
    public async Task Start_ASentenceThatWaits_StillWaitsAfterARestart_AndKeepsItsCell()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(_bruno.Id));
        _data.Upserted.Add(
            new JailSentenceEntity { Id = _bruno.Id, Name = "Bruno", IsPlayer = true, Cell = 1, Days = 2, Pending = true }
        );
        var jail = await CreateAsync(_file);

        jail.Check();

        Assert.True(Assert.Single(jail.Sentences).Pending);
        Assert.Equal("Bruno", jail.GetOccupant(1)?.Name);
        Assert.Equal(JailResultType.CellOccupied, jail.Jail(_aria, 1, 1, _staff));
        Assert.Empty(jail.Capture());
    }

    [Fact]
    public async Task Pardon_ASentenceThatNeverBegan_DropsIt_WithNoTeleportNoFineAndNoNote()
    {
        var gold = Gold(Backpack(_aria), 800);
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 3, _staff);
        Logged();

        Assert.True(_jail.Pardon(aria));

        Assert.Empty(_jail.Sentences);
        Assert.Null(_jail.GetOccupant(2));
        Assert.Equal([aria], _jail.Capture());
        Assert.Equal(["The sentence of Aria (0x00000002) for cell 2 is dropped before it began"], Logged());

        // Back in the world, it is nobody's prisoner.
        await LoginAsync(_aria, MapType.Trammel, new Point3D(1600, 1600, 5));
        _jail.Check();

        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_speech.Told);
        Assert.Equal(800, gold.Amount);
    }

    // Jailed while online, logged out in its cell, then given another cell while offline and released before it came
    // back: it stands in the first cell and must leave it.
    [Fact]
    public async Task Pardon_ARunningSentenceMadeToWait_ReleasesThePrisonerAtItsLogin()
    {
        _jail.Jail(_aria, 1, 3, _staff);
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 5, _staff);

        Assert.True(_jail.Pardon(aria));

        Assert.Null(_jail.GetOccupant(2));
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_aria));
        _jail.Check();

        Assert.Empty(_jail.Sentences);
        Assert.Equal((_aria, MapType.Trammel, new Point3D(1600, 1600, 5)), _teleports.Teleports[^1]);
        Assert.Equal("You have been released from jail.", _speech.Told[^1].Text);
    }

    [Fact]
    public async Task Check_ARunningSentenceMadeToWait_AtTheLogin_KeepsTheFirstReturnPlace()
    {
        _jail.Jail(_aria, 1, 3, _staff);
        var aria = await OfflineAsync(_aria);
        _jail.JailOffline(aria, 2, 5, _staff);
        _clock.Advance(TimeSpan.FromDays(1));
        // Where it logged out: its first cell.
        await LoginAsync(_aria, MapType.Felucca, Cell1);

        _jail.Check();

        var sentence = Assert.Single(_jail.Sentences);
        Assert.Equal((_aria, MapType.Felucca, Cell2), _teleports.Teleports[^1]);
        Assert.Equal((MapType.Trammel, 1600, 1600, 5), (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ));
        Assert.Equal(_clock.Now.ToUnixTimeMilliseconds() + 5 * Day, sentence.ReleaseAt);
    }

    [Fact]
    public void Pardon_SomeoneNotJailed_IsFalse()
    {
        Assert.False(_jail.Pardon(_aria.Id));
    }

    private ItemEntity Item(string template, int graphic, int amount)
    {
        return new() { Id = new Serial(_nextItem++), TemplateId = template, ItemId = graphic, Amount = amount };
    }

    private ItemEntity Backpack(MobileEntity owner)
    {
        var backpack = Item("backpack", 0x0E75, 1);
        backpack.Equip(owner.Id, LayerType.Backpack);
        _items.Add([backpack]);

        return backpack;
    }

    private ItemEntity Bank(MobileEntity owner)
    {
        var box = Item("bank", 0x0E7C, 1);
        box.Equip(owner.Id, LayerType.Bank);
        _items.Add([box]);

        return box;
    }

    private ItemEntity Gold(ItemEntity container, int amount)
    {
        var gold = Item("gold", 0x0EED, amount);
        gold.PutInContainer(container.Id, new Point2D(20, 20));
        _items.Add([gold]);

        return gold;
    }

    // A player character of the world database, with its account: it is not in the world.
    private MobileEntity Character(uint id, string name, string account, AccountType rank)
    {
        var owner = new Serial(5000 + id);
        var character = new MobileEntity { Id = new Serial(id), Name = name, AccountId = owner, Map = MapType.Trammel };
        _characters.Upserted.Add(character);
        _accounts.Upserted.Add(new AccountEntity { Id = owner, Username = account, AccountType = rank });

        return character;
    }

    // The player logs out and the jail finds it by its name, as .jail <name> does.
    private async Task<Serial> OfflineAsync(MobileEntity player, AccountType rank = AccountType.Regular)
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.LeaveWorld(player.Id));
        Character(player.Id.Value, player.Name, "mario", rank);
        Assert.Single(await _jail.FindAsync(player.Name));

        return player.Id;
    }

    // The player comes back into the world at that place.
    private async Task LoginAsync(MobileEntity player, MapType map, Point3D location)
    {
        player.Map = map;
        player.Location = location;
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(player));
    }

    private async Task<MobileEntity> AddPlayerAsync(long id, string name, AccountType rank)
    {
        var session = await _fixture.AddAsync(id);
        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountType, rank));
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)id), out var mobile));
        mobile.Name = name;
        mobile.AccountId = new Serial((uint)(1000 + id));

        return mobile;
    }

    private async Task<JailService> CreateAsync(JailFile? file)
    {
        var loader = new StubDataLoaderService();

        if (file is not null)
        {
            loader.With(file);
        }

        var handling = new ItemHandlingService(
            _items,
            _fixture.Sessions,
            _fixture.Sender,
            _view,
            TestTooltips.Create(_items, _fixture.Mobiles),
            new FakeItemFactoryService(_templates, _tiles),
            _serials
        );
        var jail = new JailService(
            loader,
            _data,
            _characters,
            _accounts,
            _fixture.Mobiles,
            _fixture.Sessions,
            _teleports,
            _speech,
            _timers,
            _config,
            _itemsConfig,
            _items,
            handling,
            _view,
            _clock,
            logger: new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(_log).CreateLogger()
        );
        await jail.StartAsync();
        // What the start itself says is not what the tests look at.
        Logged();

        return jail;
    }

    // The lines written since the last call.
    private string[] Logged()
    {
        var lines = _log.Events.Skip(_read).Select(entry => entry.RenderMessage()).ToArray();
        _read = _log.Events.Count;

        return lines;
    }
}
