using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Mobiles;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ParalysisServiceTests
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly SettableClock _time = new();
    private readonly ParalysisService _service;
    private readonly MobileEntity _bran = new() { Id = new Serial(3), Name = "Bran", AccountId = new Serial(0x43) };

    public ParalysisServiceTests()
    {
        _service = new(_state, _timers, _time);
    }

    [Fact]
    public void Paralyze_FreezesTheMobile_ForTheTime_AndFreesItWhenTheTimeIsUp()
    {
        Assert.True(_service.Paralyze(_bran, TimeSpan.FromSeconds(27)));

        Assert.True(_bran.Frozen);
        Assert.True(_service.IsParalyzed(_bran));
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromSeconds(27), timer.Interval);
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds() + 27, _bran.GetProp<long>(ParalysisService.UntilProp));

        _timers.Fire(timer.Id);

        Assert.False(_bran.Frozen);
        Assert.False(_service.IsParalyzed(_bran));
        Assert.False(_bran.TryGetProp<long>(ParalysisService.UntilProp, out _));
    }

    [Fact]
    public void Paralyze_AMobileAlreadyFrozen_ChangesNothing()
    {
        _bran.Frozen = true;

        Assert.False(_service.Paralyze(_bran, TimeSpan.FromSeconds(10)));

        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Paralyze_ASecondTime_DoesNotExtendTheFirst()
    {
        Assert.True(_service.Paralyze(_bran, TimeSpan.FromSeconds(10)));
        Assert.False(_service.Paralyze(_bran, TimeSpan.FromSeconds(30)));

        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(_timers.Timers).Interval);
    }

    [Fact]
    public void Paralyze_ANoTime_OrADeadMobile_ChangesNothing()
    {
        Assert.False(_service.Paralyze(_bran, TimeSpan.Zero));
        _bran.Body = 0x0192;

        Assert.False(_service.Paralyze(_bran, TimeSpan.FromSeconds(5)));
        Assert.False(_bran.Frozen);
    }

    [Fact]
    public void Release_FreesTheMobileAtOnce_AndStopsTheTimer()
    {
        _service.Paralyze(_bran, TimeSpan.FromSeconds(10));

        Assert.True(_service.Release(_bran));

        Assert.False(_bran.Frozen);
        Assert.Empty(_timers.Timers);
        Assert.False(_service.Release(_bran));
    }

    [Fact]
    public void Release_OfAMobileFrozenByTheScripts_LeavesItFrozen()
    {
        _bran.Frozen = true;

        Assert.False(_service.Release(_bran));
        Assert.True(_bran.Frozen);
    }

    [Fact]
    public void Resume_OfAParalysisWhoseTimePassed_FreesTheMobile()
    {
        _service.Paralyze(_bran, TimeSpan.FromSeconds(10));
        _timers.UnregisterAllTimers();
        _time.Advance(TimeSpan.FromSeconds(11));
        var back = new MobileEntity { Id = _bran.Id, AccountId = _bran.AccountId, Frozen = true };
        back.SetProp(ParalysisService.UntilProp, _bran.GetProp<long>(ParalysisService.UntilProp));

        new ParalysisService(_state, _timers, _time).Resume(back);

        Assert.False(back.Frozen);
        Assert.False(back.TryGetProp<long>(ParalysisService.UntilProp, out _));
    }

    [Fact]
    public void Resume_OfAParalysisWithTimeLeft_TimesItForWhatIsLeft()
    {
        _service.Paralyze(_bran, TimeSpan.FromSeconds(10));
        _timers.UnregisterAllTimers();
        _time.Advance(TimeSpan.FromSeconds(4));

        new ParalysisService(_state, _timers, _time).Resume(_bran);

        Assert.True(_bran.Frozen);
        Assert.Equal(TimeSpan.FromSeconds(6), Assert.Single(_timers.Timers).Interval);
    }

    [Fact]
    public void Resume_OfAMobileThatWasNeverParalyzed_ChangesNothing()
    {
        _bran.Frozen = true;

        _service.Resume(_bran);

        Assert.True(_bran.Frozen);
        Assert.Empty(_timers.Timers);
    }
}
