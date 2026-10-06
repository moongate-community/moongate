using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Gumps;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MurderServiceTests : IAsyncLifetime
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubGumpTemplateService _gumps = new();
    private readonly MurderConfig _config = new();
    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _ariaSession = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _boris = null!;
    private MobileEntity _carla = null!;
    private MurderService _murders = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _ariaSession = await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        await _fixture.AddAsync(4);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out _boris!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(4), out _carla!));

        foreach (var player in new[] { _aria, _boris, _carla })
        {
            player.AccountId = new Serial(0x40 + player.Id.Value);
            player.Name = player.Id.Value switch { 2 => "Aria", 3 => "Boris", _ => "Carla" };
        }

        _gumps.Ids.Add(MurderService.ReportGump);
        _murders = new(_timers, _fixture.Sessions, _fixture.Mobiles, _state, _view, _speech, _gumps, _config, _clock);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_RegistersOneRepeatingTimerOfFiveMinutes_AndStopAsyncTakesItAway()
    {
        await _murders.StartAsync();

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((MurderService.DecayTimerName, TimeSpan.FromMinutes(5), true), (timer.Name, timer.Interval, timer.Repeat));

        await _murders.StopAsync();

        Assert.Equal([timer.Id], _timers.Unregistered);
    }

    [Fact]
    public void Report_AddsAKillAndAShortTermMurder_LowersKarma_AndTellsTheKiller()
    {
        Assert.True(_murders.Report(_aria, _boris));

        Assert.Equal((1, 1, -1000), (_boris.Kills, _boris.ShortTermMurders, _boris.Karma));
        Assert.Equal(MurderService.ReportedCliloc, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.False(_boris.IsMurderer);
        Assert.DoesNotContain(_view.Calls, call => call.StartsWith("FlagsChanged", StringComparison.Ordinal));
    }

    [Fact]
    public void Report_TheFifthKill_MakesAMurderer_TellsItSo_AndRedrawsItsName()
    {
        _boris.Kills = 4;

        Assert.True(_murders.Report(_aria, _boris));

        Assert.True(_boris.IsMurderer);
        Assert.Equal([MurderService.ReportedCliloc, MurderService.MurdererCliloc], _speech.ToldClilocs.Select(told => told.Cliloc));
        Assert.Contains($"FlagsChanged {_boris.Id.Value}", _view.Calls);
    }

    [Fact]
    public void Report_TheSameKillerAgainWithinTenMinutes_CountsNothing_ButAfterThemItDoes()
    {
        Assert.True(_murders.Report(_aria, _boris));
        Assert.False(_murders.Report(_aria, _boris));
        // Another victim can report it at once.
        Assert.True(_murders.Report(_carla, _boris));

        _clock.Advance(TimeSpan.FromMinutes(11));

        Assert.True(_murders.Report(_aria, _boris));
        Assert.Equal(3, _boris.Kills);
    }

    [Fact]
    public void Report_AnNpc_OrAKillerOutOfTheWorld_CountsNothing()
    {
        var orc = new MobileEntity { Id = new Serial(900), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel };
        _fixture.Mobiles.EnterWorld(orc);

        Assert.False(_murders.Report(_aria, orc));
        Assert.False(_murders.Report(_aria, new MobileEntity { Id = new Serial(901), AccountId = new Serial(9) }));
    }

    [Fact]
    public void Report_SetsWhenEachCountIsForgotten()
    {
        _murders.Report(_aria, _boris);

        var now = _clock.GetUtcNow().UtcDateTime;
        Assert.Equal((now.AddHours(40), now.AddHours(8)), (_boris.KillsDecayAt, _boris.ShortTermDecayAt));
    }

    [Fact]
    public void Died_AsksTheVictimAboutWhoAttackedItAsACriminal_AfterTheDelay_OneAtATime()
    {
        _murders.Aggressed(_boris, _aria);
        _murders.Aggressed(_carla, _aria);

        _murders.Died(_aria);

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((MurderService.AskTimerName, TimeSpan.FromSeconds(4)), (timer.Name, timer.Interval));
        Assert.Empty(_gumps.Opened);

        _timers.Fire(timer.Id);

        var first = Assert.Single(_gumps.Opened);
        Assert.Equal((MurderService.ReportGump, "Boris"), (first.Id, first.Args["name"]));
        Assert.Same(_ariaSession, first.Session);
    }

    [Fact]
    public void Yes_ReportsTheKiller_AndTheNextIsAsked_NoAndClosingReportNobody()
    {
        _murders.Aggressed(_boris, _aria);
        _murders.Aggressed(_carla, _aria);
        _murders.Died(_aria);
        _timers.Fire(_timers.Timers.Single().Id);

        _gumps.Opened[0].OnAnswer(_ariaSession, Answer(MurderService.YesClick));

        Assert.Equal(1, _boris.Kills);
        Assert.Equal("Carla", _gumps.Opened[1].Args["name"]);

        _gumps.Opened[1].OnClosed!(_ariaSession, GumpCloseReasonType.Server);

        Assert.Equal(0, _carla.Kills);
        Assert.Equal(2, _gumps.Opened.Count);
    }

    [Fact]
    public void No_ReportsNobody()
    {
        _murders.Aggressed(_boris, _aria);
        _murders.Died(_aria);
        _timers.Fire(_timers.Timers.Single().Id);

        _gumps.Opened[0].OnAnswer(_ariaSession, Answer("no"));

        Assert.Equal(0, _boris.Kills);
    }

    [Fact]
    public void AnAttackThatIsTooOld_OrOnAnNpc_OrByAnNpc_IsNotAsked()
    {
        _murders.Aggressed(_boris, _aria);
        _clock.Advance(TimeSpan.FromSeconds(121));
        _murders.Died(_aria);

        Assert.Empty(_timers.Timers);

        var orc = new MobileEntity { Id = new Serial(900), TemplateId = "orc" };
        _murders.Aggressed(orc, _aria);
        _murders.Aggressed(_boris, orc);
        _murders.Died(_aria);
        _murders.Died(orc);

        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void ADeathWithoutAttackers_AsksNobody()
    {
        _murders.Died(_aria);

        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public async Task TheDecayTimer_ForgetsTheCountsWhoseTimeIsOver_OneForEachPeriod_AndRedrawsWhoStopsBeingRed()
    {
        _boris.Kills = 5;
        _boris.ShortTermMurders = 2;
        var now = _clock.GetUtcNow().UtcDateTime;
        _boris.KillsDecayAt = now.AddHours(1);
        _boris.ShortTermDecayAt = now.AddHours(1);
        await _murders.StartAsync();

        _clock.Advance(TimeSpan.FromHours(2));
        _timers.Fire(_timers.Timers.Single().Id);

        // The short term lasts eight hours: one is gone now, the other in eight; the kills last forty.
        Assert.Equal((4, 1), (_boris.Kills, _boris.ShortTermMurders));
        Assert.Equal(now.AddHours(41), _boris.KillsDecayAt);
        Assert.Contains($"FlagsChanged {_boris.Id.Value}", _view.Calls);

        _clock.Advance(TimeSpan.FromHours(7));
        _murders.Restore(_boris);

        Assert.Equal((4, 0, (DateTime?)null), (_boris.Kills, _boris.ShortTermMurders, _boris.ShortTermDecayAt));
    }

    private static GumpTemplateAnswer Answer(string click)
    {
        return new GumpTemplateAnswer
        {
            Click = click,
            Bound = new Dictionary<string, object>(),
            Response = new GumpResponse
            {
                ButtonId = 1, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>()
            }
        };
    }
}
