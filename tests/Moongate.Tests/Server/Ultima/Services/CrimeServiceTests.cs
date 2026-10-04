using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class CrimeServiceTests : IAsyncLifetime
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly CrimeConfig _config = new();
    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;
    private MobileEntity _aria = null!;
    private CrimeService _crimes = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _crimes = new(_timers, _fixture.Sessions, _fixture.Mobiles, _view, _speech, _config, _clock);
        await _crimes.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _crimes.StopAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void StartAsync_RegistersOneRepeatingTimerEverySecond_AndStopAsyncRemovesIt()
    {
        var timer = Assert.Single(_timers.Timers);

        Assert.Equal(("crime", TimeSpan.FromSeconds(1), true), (timer.Name, timer.Interval, timer.Repeat));
    }

    [Fact]
    public void MakeCriminal_TurnsTheNameGrey_TellsThePlayer_AndShowsThoseAround()
    {
        _crimes.MakeCriminal(_aria);

        Assert.True(_crimes.IsCriminal(_aria));
        Assert.Equal(NotorietyType.Criminal, _aria.ShownNotoriety);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddSeconds(120), _aria.CriminalUntil);
        Assert.Equal(CrimeService.CriminalMessage, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Equal(["FlagsChanged 2"], _view.Calls);
    }

    [Fact]
    public void AfterTheTime_TheMobileIsInnocentAgain_AndThoseAroundSeeIt()
    {
        _crimes.MakeCriminal(_aria);
        _view.Calls.Clear();

        _clock.Advance(TimeSpan.FromSeconds(119));
        Fire();
        Assert.True(_crimes.IsCriminal(_aria));
        Assert.Empty(_view.Calls);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Fire();
        Assert.False(_crimes.IsCriminal(_aria));
        Assert.Equal((NotorietyType.Innocent, (DateTime?)null), (_aria.ShownNotoriety, _aria.CriminalUntil));
        Assert.Equal(["FlagsChanged 2"], _view.Calls);
    }

    [Fact]
    public void AnotherCrime_StartsTheTimeAgain_WithoutTellingTwice()
    {
        _crimes.MakeCriminal(_aria);
        _clock.Advance(TimeSpan.FromSeconds(100));

        _crimes.MakeCriminal(_aria);
        _clock.Advance(TimeSpan.FromSeconds(100));
        Fire();

        Assert.True(_crimes.IsCriminal(_aria));
        Assert.Single(_speech.ToldClilocs);
        Assert.Equal(["FlagsChanged 2"], _view.Calls);
    }

    [Fact]
    public void Pardon_ClearsItAtOnce_AndAnInnocentIsLeftAlone()
    {
        _crimes.Pardon(_aria);
        Assert.Empty(_view.Calls);

        _crimes.MakeCriminal(_aria);
        _crimes.Pardon(_aria);

        Assert.False(_crimes.IsCriminal(_aria));
        Assert.Null(_aria.CriminalUntil);
        Assert.Equal(["FlagsChanged 2", "FlagsChanged 2"], _view.Calls);
    }

    [Fact]
    public void AMurderer_StaysRed_WhileItIsAlsoACriminal()
    {
        _aria.Notoriety = NotorietyType.Murderer;

        _crimes.MakeCriminal(_aria);

        Assert.Equal(NotorietyType.Murderer, _aria.ShownNotoriety);
    }

    [Fact]
    public void APlayerThatComesBackWithTimeLeft_IsGreyAgain_AndOneWhoseTimeRanOutIsNot()
    {
        // As its row says after a relogin: the time is saved, the flag is not.
        _aria.CriminalUntil = _clock.GetUtcNow().UtcDateTime.AddSeconds(30);

        Fire();

        Assert.True(_crimes.IsCriminal(_aria));
        Assert.Equal(["FlagsChanged 2"], _view.Calls);
        // It already knows what it did.
        Assert.Empty(_speech.ToldClilocs);

        _aria.Criminal = false;
        _aria.CriminalUntil = _clock.GetUtcNow().UtcDateTime.AddSeconds(-5);
        _view.Calls.Clear();
        Fire();

        Assert.False(_crimes.IsCriminal(_aria));
        Assert.Null(_aria.CriminalUntil);
    }

    [Fact]
    public void AnNpc_CanBeACriminalToo_AndIsNotTold()
    {
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "an orc", TemplateId = "orc" };
        _fixture.Mobiles.EnterWorld(orc);

        _crimes.MakeCriminal(orc);
        Assert.Equal(NotorietyType.Criminal, orc.ShownNotoriety);

        _clock.Advance(TimeSpan.FromSeconds(120));
        Fire();
        Assert.False(orc.Criminal);
    }

    [Fact]
    public void ASnapshot_SavesTheTime_NotTheFlag()
    {
        _crimes.MakeCriminal(_aria);

        var saved = _aria.Snapshot();

        Assert.False(saved.Criminal);
        Assert.Equal(_aria.CriminalUntil, saved.CriminalUntil);
    }

    private void Fire()
    {
        _timers.Fire(_timers.Timers[0].Id);
    }
}
