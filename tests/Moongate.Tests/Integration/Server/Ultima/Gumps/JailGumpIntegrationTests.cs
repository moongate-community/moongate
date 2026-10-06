using DryIoc;
using Lua;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the jail gump shipped in <c>moongate_root</c> (templates/gumps/jail_sentence.xml and
///     scripts/gumps/jail_sentence.lua) and the release note script (scripts/items/jail_note.lua) with the real Lua
///     engine: the cells the gump lists and what its buttons do.
/// </summary>
public sealed class JailGumpIntegrationTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long Target = 9;

    // A player found by .jail <name> who is not in the world.
    private const long Offline = 200;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubJailService _jail = new();
    private readonly SettableClock _clock = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly RecordingTeleportService _teleports = new();
    private readonly StubTargetService _targets = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private LuaScriptEngineService _engine = null!;
    private ScriptEngineOptions _options = null!;
    private ItemService _items = null!;
    private GumpModule _module = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(Staff);
        await _fixture.AddAsync(Player);
        await _fixture.AddAsync(Target);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        _jail.Now = _clock.Now.ToUnixTimeMilliseconds();

        for (var number = 1; number <= 12; number++)
        {
            _jail.CellList.Add(new() { Number = number, Location = new Point3D(5276 + number, 1164, 0) });
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write(
            "gumps/jail_sentence.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "jail_sentence.lua"))
        );
        _scripts.Write(
            "items/jail_note.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "jail_note.lua"))
        );
        var templates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        _options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        _items = TestItems.Create(_fixture.Sectors);

        GumpScriptService? gumpScripts = null;
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(new RecordingWorldViewService());
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ITeleportService>(_teleports);
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.RegisterInstance<IJailService>(_jail);
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.AddScriptModule<LogModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<MobileModule>();
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        var noteTemplates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "jail_release_note", ScriptId = "jail_note", Stackable = false }
            )
        );
        _container.RegisterInstance<IBookDocumentService>(
            await TestBookDocuments.CreateAsync(
                _fixture,
                _items,
                _container.Resolve<IItemHandlingService>(),
                noteTemplates,
                _loop,
                _gumps
            )
        );
        _container.AddScriptModule<BookModule>();
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<JailModule>();
        _container.AddScriptModule<TargetModule>();
        _container.RegisterScriptEnum<JailResultType>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _engine = new(
            _options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, _options);
        await gumpScripts.StartAsync();
        _module = _container.Resolve<GumpModule>();
    }

    [Fact]
    public void Open_ShowsTheTargetTheDaysAndEveryCell_FreeAndOccupied()
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 2, secondsLeft: 2 * 86400 + 4 * 3600 + 120));

        var built = Open(Staff);

        Assert.Contains("Target: Lord Pippo", built.Strings);
        Assert.Contains("1", built.Strings);
        Assert.Contains("Cell 1", built.Strings);
        Assert.Contains("Cell 12", built.Strings);
        Assert.Contains("Gino - 2d 4h left", built.Strings);
        Assert.Contains("free", built.Strings);
        // The target button, then two per cell: eleven that jail and one that releases, and a go for each.
        Assert.Equal(25, built.Buttons.Count);
        Assert.Contains("{ textentrylimited ", built.Layout);
        // Ten cells a page.
        Assert.Contains("{ page 2 }", built.Layout);
        Assert.Empty(_errors);
    }

    [Theory,
     InlineData(5 * 3600 + 10 * 60 + 30, "5h 10m left"),
     InlineData(12 * 60 + 59, "12m left"),
     InlineData(20, "1m left")]
    public void Open_ShowsTheTimeLeftInItsLargestUnits(long seconds, string left)
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 1, secondsLeft: seconds));

        Assert.Contains("Gino - " + left, Open(Staff).Strings);
    }

    [Fact]
    public void AFreeCell_WithGoodDays_JailsTheTargetThere_AndTellsTheGameMaster()
    {
        Open(Staff);

        Answer(0, Jail(3), "5");

        var (prisoner, cell, days, by) = Assert.Single(_jail.Jailed);
        Assert.Equal((new Serial((uint)Target), 3, 5, new Serial((uint)Staff)), (prisoner.Id, cell, days, by.Id));
        Assert.Equal("Lord Pippo is in cell 3 for 5 days.", Assert.Single(_speech.Told).Text);
        Assert.Single(_gumps.Opened);
        Assert.Empty(_errors);
    }

    [Theory, InlineData(""), InlineData("abc"), InlineData("0"), InlineData("-3"), InlineData("2.5"),
     InlineData("999999999999"), InlineData("31")]
    public void AFreeCell_WithBadDays_JailsNobody_SaysWhy_AndOpensTheGumpAgain(string days)
    {
        Open(Staff);

        Answer(0, Jail(1), days);

        Assert.Empty(_jail.Jailed);
        Assert.Equal("Type the days as a whole number from 1 to 30.", Assert.Single(_speech.Told).Text);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AFreeCell_TakenMeanwhile_SaysTaken_AndOpensTheGumpAgainWithTheDaysTyped()
    {
        Open(Staff);
        _jail.Result = JailResultType.CellOccupied;

        Answer(0, Jail(1), "4");

        Assert.Equal("That cell is taken.", Assert.Single(_speech.Told).Text);
        Assert.Contains("4", _gumps.Opened[1].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Release_PardonsThePrisonerOfTheCell_AndOpensTheGumpAgain()
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 2, secondsLeft: 3600));
        Open(Staff);

        // The cell that holds someone has its release where a free one has its jail button.
        Answer(0, Jail(2), "1");

        Assert.Equal([new Serial((uint)Player)], _jail.Pardoned);
        Assert.Equal("Gino is released.", Assert.Single(_speech.Told).Text);
        Assert.Empty(_jail.Jailed);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ATargetAlreadyInJail_SeesItsOwnCellAsItsOwn_AndCanBeReleasedFromTheTop()
    {
        _jail.SentenceList.Add(Sentence(Target, "Lord Pippo", cell: 2, secondsLeft: 3600));

        var built = Open(Staff);

        Assert.Contains("In cell 2, 1h 0m left", built.Strings);
        Assert.Contains("here now", built.Strings);

        // After the target button comes the release of the target; the cells follow.
        Answer(0, 2, "1");

        Assert.Equal([new Serial((uint)Target)], _jail.Pardoned);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AnOfflineTarget_IsSaidOffline()
    {
        var built = OpenWith(Staff, OfflineTarget());

        Assert.Contains("Target: Pippo (offline)", built.Strings);
        // It can be jailed: a jail button and a go for each cell.
        Assert.Equal(25, built.Buttons.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void SeveralOfTheName_AreListed_WithTheirAccount_AndWhetherTheyAreOnline()
    {
        var built = OpenWith(Staff, Homonyms((Player, "mario"), (Offline, "luigi")));

        Assert.Contains("Target: nobody. Pick one of these, or press the button.", built.Strings);
        Assert.Contains("Pippo - account mario - online", built.Strings);
        Assert.Contains("Pippo - account luigi - offline", built.Strings);
        // The target button and one for each of the two. The cells come once one is picked: the list has their place.
        Assert.Equal(3, built.Buttons.Count);
        Assert.DoesNotContain("Cell 1", built.Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void PickingOneOfTheList_MakesItTheTarget_AndTheListIsGone()
    {
        OpenWith(Staff, Homonyms((Player, "mario"), (Offline, "luigi")));

        // After the target button come the characters of the list, in its order.
        Answer(0, 3, "7", "Stole a horse");

        var again = _gumps.Opened[1].Gump.Layout.Build();
        Assert.Contains("Target: Pippo (offline)", again.Strings);
        // What was typed is kept.
        Assert.Contains("7", again.Strings);
        Assert.Contains("Stole a horse", again.Strings);
        Assert.DoesNotContain(again.Strings, text => text.Contains("account"));
        Assert.Equal(25, again.Buttons.Count);

        _jail.OfflineResult = JailResultType.Pending;
        Answer(1, Jail(4), "7", "Stole a horse");

        Assert.Equal(
            (new Serial((uint)Offline), 4, 7),
            (_jail.JailedOffline[0].Prisoner, _jail.JailedOffline[0].Cell, _jail.JailedOffline[0].Days)
        );
        Assert.Empty(_errors);
    }

    // The cursor instead of the list, then put away: the list is still there to pick from.
    [Fact]
    public void TheList_ComesBackWhenTheTargetCursorIsPutAway()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);
        OpenWith(Staff, Homonyms((Player, "mario"), (Offline, "luigi")));

        Answer(0, TargetButton, "1");

        var again = _gumps.Opened[1].Gump.Layout.Build();
        Assert.Contains("Pippo - account luigi - offline", again.Strings);
        Assert.Equal(3, again.Buttons.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void MoreThanTenOfTheName_SaysHowManyMore()
    {
        var many = Enumerable.Range(0, 12).Select(index => (Offline + index, "account" + index)).ToArray();

        var built = OpenWith(Staff, Homonyms(many));

        Assert.Contains("Pippo - account account9 - offline", built.Strings);
        Assert.DoesNotContain("Pippo - account account10 - offline", built.Strings);
        Assert.Contains("and 2 more of that name", built.Strings);
        Assert.Equal(1 + 10, built.Buttons.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AFreeCell_ForAnOfflineTarget_KeepsTheSentenceWaiting_AndTellsTheGameMaster()
    {
        _jail.OfflineResult = JailResultType.Pending;
        OpenWith(Staff, OfflineTarget());

        Answer(0, Jail(3), "3", "Stole a horse");

        var sent = Assert.Single(_jail.JailedOffline);
        Assert.Equal(
            (new Serial((uint)Offline), 3, 3, new Serial((uint)Staff)),
            (sent.Prisoner, sent.Cell, sent.Days, sent.By.Id)
        );
        Assert.Equal(["Stole a horse"], _jail.Reasons);
        Assert.Equal("Pippo will be in cell 3 for 3 days from its next login.", Assert.Single(_speech.Told).Text);
        // Done: the gump does not come back.
        Assert.Single(_gumps.Opened);
        Assert.Empty(_errors);
    }

    // It was deleted, or the jail was restarted since .jail <name> found it.
    [Fact]
    public void AFreeCell_ForAnOfflineTargetTheJailNoLongerKnows_SaysSo_AndOpensTheGumpAgain()
    {
        OpenWith(Staff, OfflineTarget());

        Answer(0, Jail(3), "3");

        Assert.Equal("That character is no longer here.", Assert.Single(_speech.Told).Text);
        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ACellKeptForASentenceThatWaits_SaysSo_AndCanBeReleased()
    {
        _jail.SentenceList.Add(Waiting(Player, "Gino", cell: 2));

        var built = Open(Staff);

        Assert.Contains("Gino - waits for login", built.Strings);
        Assert.DoesNotContain(built.Strings, text => text.StartsWith("Gino") && text.Contains("left"));

        Answer(0, Jail(2), "1");

        Assert.Equal([new Serial((uint)Player)], _jail.Pardoned);
        Assert.Equal("Gino is released.", Assert.Single(_speech.Told).Text);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ATargetWhoseSentenceWaits_SeesItAtTheTop_AndCanBeReleased()
    {
        _jail.SentenceList.Add(Waiting(Offline, "Pippo", cell: 2));

        var built = OpenWith(Staff, OfflineTarget());

        Assert.Contains("Waits for login: cell 2, 3 days", built.Strings);
        // Nobody is in its cell yet.
        Assert.Contains("kept for it", built.Strings);
        Assert.DoesNotContain("here now", built.Strings);

        // After the target button comes the release of the target; the cells follow.
        Answer(0, 2, "1");

        Assert.Equal([new Serial((uint)Offline)], _jail.Pardoned);
        Assert.Empty(_errors);
    }

    // Its sentence is over and it is offline: the jail releases it when it logs in.
    [Fact]
    public void ATargetWhoseSentenceIsOver_IsSaidToWaitForItsLogin_NotToHaveAMinuteLeft()
    {
        _jail.SentenceList.Add(Sentence(Target, "Lord Pippo", cell: 2, secondsLeft: 0));

        var built = Open(Staff);

        Assert.Contains("Sentence over: free at its next login", built.Strings);
        Assert.DoesNotContain(built.Strings, text => text.Contains("left"));
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlayer_SeesNoCell()
    {
        var built = Open(Player);

        Assert.DoesNotContain("Cell 1", built.Strings);
        Assert.Empty(built.Buttons);
        Assert.Empty(_errors);
    }

    // The account may lose its rank while the gump is open.
    [Fact]
    public async Task ACellClickedByOneWhoIsNoLongerStaff_DoesNothing()
    {
        Open(Staff);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        Answer(0, Jail(1), "3");
        Answer(0, Go(1), "3");
        Answer(0, TargetButton, "3");

        Assert.Empty(_jail.Jailed);
        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheReasonTyped_GoesWithTheSentence_AndAnEmptyOneIsNone()
    {
        Open(Staff);
        Answer(0, Jail(3), "5", "Stole a horse");
        Open(Staff);
        Answer(1, Jail(4), "5");

        Assert.Equal(["Stole a horse", null], _jail.Reasons);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheReasonTyped_IsKeptWhenTheGumpOpensAgain()
    {
        Open(Staff);

        // Days that jail nobody: the gump comes back with what was typed.
        Answer(0, Jail(1), "abc", "Stole a horse");
        Assert.Contains("Stole a horse", _gumps.Opened[1].Gump.Layout.Build().Strings);

        // And after a visit to a cell.
        Answer(1, Go(2), "5", "Insulted the king");
        Assert.Contains("Insulted the king", _gumps.Opened[2].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ATargetAlreadyInJail_ComesWithItsReason_SoMovingItKeepsIt()
    {
        var sentence = Sentence(Player, "Gino", cell: 2, secondsLeft: 3600);
        sentence.Reason = "Stole a horse";
        _jail.SentenceList.Add(sentence);
        _targets.Result = TargetResult.ForObject(new Serial((uint)Player));
        Open(Staff, false);

        Answer(0, TargetButton, "1");

        // The field shows why it is in jail.
        Assert.Contains("Stole a horse", _gumps.Opened[1].Gump.Layout.Build().Strings);

        // A reason typed before the pick wins over the one of the sentence.
        Answer(0, TargetButton, "1", "Insulted the king");
        Assert.Contains("Insulted the king", _gumps.Opened[2].Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void WithoutATarget_TheCellsAreListed_ButNobodyCanBeJailed()
    {
        _jail.SentenceList.Add(Sentence(Player, "Gino", cell: 2, secondsLeft: 3600));

        var built = Open(Staff, false);

        Assert.Contains("Target: nobody. Press the button to pick one.", built.Strings);
        Assert.Contains("Gino - 1h 0m left", built.Strings);
        Assert.Contains("free", built.Strings);
        // The target button, a go for each cell and the release of Gino: no button that jails.
        Assert.Equal(14, built.Buttons.Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Go_TakesTheGameMasterIntoTheCell_OnTheMapOfTheJail_AndOpensTheGumpAgain()
    {
        Open(Staff);

        Answer(0, Go(3), "5");

        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Staff), out var staff));
        Assert.Equal((staff, MapType.Felucca, new Point3D(5279, 1164, 0)), Assert.Single(_teleports.Teleports));
        Assert.Empty(_jail.Jailed);
        // With the same target and the days that were typed.
        var again = _gumps.Opened[1].Gump.Layout.Build().Strings;
        Assert.Contains("Target: Lord Pippo", again);
        Assert.Contains("5", again);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Go_WorksWithoutATarget_ToVisitTheCells()
    {
        Open(Staff, false);

        // Target, then one go per cell: no cell holds anyone.
        Answer(0, 1 + 4, "1");

        Assert.Equal(new Point3D(5280, 1164, 0), Assert.Single(_teleports.Teleports).Location);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheTargetButton_GivesTheCursor_AndTheCharacterPickedBecomesTheOneToJail()
    {
        _targets.Result = TargetResult.ForObject(new Serial((uint)Player));
        Open(Staff, false);

        Answer(0, TargetButton, "7");

        var again = _gumps.Opened[1].Gump.Layout.Build();
        Assert.Contains("Target: Player", again.Strings);
        Assert.Contains("7", again.Strings);
        // Now it can be jailed: a jail button and a go for each cell.
        Assert.Equal(25, again.Buttons.Count);

        Answer(1, Jail(4), "7");
        Assert.Equal(
            (new Serial((uint)Player), 4, 7),
            (_jail.Jailed[0].Prisoner.Id, _jail.Jailed[0].Cell, _jail.Jailed[0].Days)
        );
        Assert.Empty(_errors);
    }

    // A second .jail, or any other command with a cursor, takes the first cursor away: its gump must not come back
    // on top of the new one.
    [Theory, InlineData(TargetCancelType.Overridden), InlineData(TargetCancelType.Disconnected)]
    public void TheTargetButton_WhoseCursorIsTakenAway_DoesNotOpenTheGumpAgain(TargetCancelType reason)
    {
        _targets.Result = TargetResult.Canceled(reason);
        Open(Staff);

        Answer(0, TargetButton, "1");

        Assert.Single(_gumps.Opened);
        Assert.Empty(_speech.Told);
        Assert.Empty(_errors);
    }

    [Theory, InlineData("item"), InlineData("gone"), InlineData("ground"), InlineData("canceled")]
    public void TheTargetButton_OnWhatIsNotACharacter_KeepsTheTargetItHad(string what)
    {
        _targets.Result = what switch
        {
            "item"   => TargetResult.ForObject(new Serial(0x40000001)),
            "gone"   => TargetResult.ForObject(new Serial(777)),
            "ground" => TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0)),
            _        => TargetResult.Canceled(TargetCancelType.Canceled)
        };
        Open(Staff);

        Answer(0, TargetButton, "1");

        Assert.Contains("Target: Lord Pippo", _gumps.Opened[1].Gump.Layout.Build().Strings);
        // A cursor put away says nothing; a wrong pick says why.
        Assert.Equal(what == "canceled" ? [] : ["That is not a character."], _speech.Told.Select(told => told.Text));
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task TheNote_OnADoubleClick_ShowsItsText()
    {
        var note = new ItemEntity
        {
            Id = new Serial(0x40000001), TemplateId = "jail_release_note", ItemId = 0x14F0, Amount = 1,
            Props = new() { ["jail.text"] = "Lord Pippo served 3 days in cell 2." }
        };
        note.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([note]);
        await _fixture.Network.ExecuteOnLoopAsync(() =>
            {
                Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Staff), out var reader));
                _fixture.Mobiles.MoveTo(reader, MapType.Trammel, new Point3D(1600, 1600, 0));
            }
        );
        var scripts = new ItemScriptService(
            _engine,
            new ItemTemplateService(
                new StubDataLoaderService().With(new ItemTemplate { Id = "jail_release_note", ScriptId = "jail_note" })
            ),
            _loop,
            _options
        );
        await scripts.StartAsync();

        var used = scripts.Run(note, "on_use", Staff);
        Assert.Equal(true, Assert.Single(used.Values));

        Assert.Contains("Lord Pippo served 3 days in cell 2.", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private JailSentenceEntity Sentence(long prisoner, string name, int cell, long secondsLeft)
    {
        return new()
        {
            Id = new Serial((uint)prisoner), Name = name, Cell = cell, Days = 3, JailedBy = "Giachi",
            ReleaseAt = _jail.Now + secondsLeft * 1000
        };
    }

    // The buttons answer in the order the script makes them: the target button, then for each cell its jail button
    // (or the release of who is inside) and its go. So it is with a target and nobody of its own in jail.
    private const int TargetButton = 1;

    private static int Jail(int cell)
    {
        return 2 * cell;
    }

    private static int Go(int cell)
    {
        return 2 * cell + 1;
    }

    private JailSentenceEntity Waiting(long prisoner, string name, int cell)
    {
        return new()
        {
            Id = new Serial((uint)prisoner), Name = name, Cell = cell, Days = 3, JailedBy = "Giachi", Pending = true
        };
    }

    // What .jail Pippo opens the gump with when it finds one player, who is offline.
    private static LuaTable OfflineTarget()
    {
        var args = new LuaTable();
        args["target"] = Offline;
        args["name"] = "Pippo";
        args["days"] = "1";

        return args;
    }

    // What .jail Pippo opens the gump with when several players have the name.
    private static LuaTable Homonyms(params (long Serial, string Account)[] characters)
    {
        var candidates = new LuaTable();

        for (var index = 0; index < characters.Length; index++)
        {
            var entry = new LuaTable();
            entry["serial"] = characters[index].Serial;
            entry["name"] = "Pippo";
            entry["account"] = characters[index].Account;
            candidates[index + 1] = entry;
        }

        var args = new LuaTable();
        args["candidates"] = candidates;
        args["days"] = "1";

        return args;
    }

    private GumpBuildResult OpenWith(long player, LuaTable args)
    {
        Assert.True(_module.Open(player, "jail_sentence", args));

        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    private GumpBuildResult Open(long player, bool withTarget = true)
    {
        var args = new LuaTable();

        if (withTarget)
        {
            args["target"] = Target;
            args["name"] = "Lord Pippo";
        }

        args["days"] = "1";

        Assert.True(_module.Open(player, "jail_sentence", args));

        return _gumps.Opened[^1].Gump.Layout.Build();
    }

    private void Answer(int gump, int button, string days, string reason = "")
    {
        // As the loop does: what the script posts runs after the script, not inside it.
        _loop.DeferTryPost = true;
        _gumps.Opened[gump]
            .Gump.OnResponse(
                _session,
                new GumpResponse
                {
                    ButtonId = button, Switches = new HashSet<int>(),
                    Texts = new Dictionary<int, string> { [1] = days, [2] = reason }
                }
            );
        _loop.RunDeferred();
        _loop.DeferTryPost = false;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
