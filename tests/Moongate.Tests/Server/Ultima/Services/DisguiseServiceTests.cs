using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Mounts;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class DisguiseServiceTests
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingMountService _mounts = new();
    private readonly SettableClock _time = new();
    private readonly DisguiseService _service;
    private readonly MobileEntity _bran = new() { Id = new Serial(3), Name = "Bran", AccountId = new Serial(0x43), Body = 400, SkinHue = new Hue(0x3EA) };

    public DisguiseServiceTests()
    {
        _service = new(_state, _timers, _time, _mounts);
    }

    [Fact]
    public void Disguise_ChangesTheBodyHueAndName_AndGivesThemBackWhenTheTimeIsUp()
    {
        Assert.True(_service.Disguise(_bran, new DisguiseLooks(Body: 0xD3, Hue: 0x3F0, Name: "Grog"), TimeSpan.FromSeconds(60)));

        Assert.Equal(("Grog", 0xD3, (ushort)0x3F0), (_bran.Name, _bran.Body, _bran.SkinHue.Value));
        Assert.True(_service.IsDisguised(_bran));
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromSeconds(60), timer.Interval);

        _timers.Fire(timer.Id);

        Assert.Equal(("Bran", 400, (ushort)0x3EA), (_bran.Name, _bran.Body, _bran.SkinHue.Value));
        Assert.False(_service.IsDisguised(_bran));
    }

    [Fact]
    public void Disguise_OfTheNameAndHueOnly_LeavesTheBody()
    {
        Assert.True(_service.Disguise(_bran, new DisguiseLooks(Hue: 0x3F0, Name: "Grog"), TimeSpan.FromSeconds(60)));

        Assert.Equal(400, _bran.Body);
        Assert.True(_service.End(_bran));
        Assert.Equal(("Bran", (ushort)0x3EA), (_bran.Name, _bran.SkinHue.Value));
    }

    [Fact]
    public void Disguise_ADisguisedMobile_IsRefused_AndNothingChanges()
    {
        _service.Disguise(_bran, new DisguiseLooks(Name: "Grog"), TimeSpan.FromSeconds(60));

        Assert.False(_service.Disguise(_bran, new DisguiseLooks(Body: 0xD3), TimeSpan.FromSeconds(60)));

        Assert.Equal((400, "Grog"), (_bran.Body, _bran.Name));
        Assert.Single(_timers.Timers);
    }

    [Fact]
    public void Disguise_OfNothingOrNoTimeOrADeadMobile_ChangesNothing()
    {
        Assert.False(_service.Disguise(_bran, new DisguiseLooks(), TimeSpan.FromSeconds(60)));
        Assert.False(_service.Disguise(_bran, new DisguiseLooks(Name: "Grog"), TimeSpan.Zero));
        _bran.Body = 0x0192;

        Assert.False(_service.Disguise(_bran, new DisguiseLooks(Name: "Grog"), TimeSpan.FromSeconds(5)));
        Assert.Equal("Bran", _bran.Name);
    }

    [Fact]
    public void Disguise_AsAnAnimal_DismountsTheRider_ButAsAManDoesNot()
    {
        _mounts.Mounted.Add(_bran.Id);

        _service.Disguise(_bran, new DisguiseLooks(Body: 401), TimeSpan.FromSeconds(60));
        Assert.Empty(_mounts.Dismounts);

        _service.End(_bran);
        _service.Disguise(_bran, new DisguiseLooks(Body: 0xD3), TimeSpan.FromSeconds(60));

        Assert.Equal(_bran, Assert.Single(_mounts.Dismounts));
    }

    [Fact]
    public void End_StopsTheTimer_AndAMobileThatWasNotDisguisedAnswersFalse()
    {
        Assert.False(_service.End(_bran));
        _service.Disguise(_bran, new DisguiseLooks(Name: "Grog"), TimeSpan.FromSeconds(60));

        Assert.True(_service.End(_bran));

        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Resume_OfADisguiseWhoseTimePassed_GivesTheMobileItsOwnLooksBack()
    {
        _service.Disguise(_bran, new DisguiseLooks(Body: 0xD3, Name: "Grog"), TimeSpan.FromSeconds(60));
        _timers.UnregisterAllTimers();
        _time.Advance(TimeSpan.FromSeconds(61));
        var service = new DisguiseService(_state, _timers, _time, _mounts);

        service.Resume(_bran);

        Assert.Equal(("Bran", 400), (_bran.Name, _bran.Body));
    }

    [Fact]
    public void Resume_OfADisguiseWithTimeLeft_TimesItForWhatIsLeft()
    {
        _service.Disguise(_bran, new DisguiseLooks(Name: "Grog"), TimeSpan.FromSeconds(60));
        _timers.UnregisterAllTimers();
        _time.Advance(TimeSpan.FromSeconds(20));
        var service = new DisguiseService(_state, _timers, _time, _mounts);

        service.Resume(_bran);

        Assert.Equal("Grog", _bran.Name);
        Assert.Equal(TimeSpan.FromSeconds(40), Assert.Single(_timers.Timers).Interval);
    }
}
