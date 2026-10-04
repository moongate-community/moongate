using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

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
    private readonly JailConfig _config = new();
    private readonly SettableClock _clock = new();
    private readonly JailFile _file = new()
    {
        Map = MapType.Felucca,
        Release = new Point3D(1444, 1697, 10),
        Cell = [new JailCell { Number = 1, Location = Cell1 }, new JailCell { Number = 2, Location = Cell2 }]
    };

    private BroadcastFixture _fixture = null!;
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

        var jail = new JailService(loader, _data, _fixture.Mobiles, _fixture.Sessions, _teleports, _speech, _timers, _config, _clock);
        await jail.StartAsync();

        return jail;
    }
}
