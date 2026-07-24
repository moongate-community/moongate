namespace Moongate.Tests.Support;

public class MutableTimeProviderTests
{
    [Fact]
    public void Advance_OneShotTimer_FiresAtDueTimeOnce()
    {
        var time = new MutableTimeProvider(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var callbackCount = 0;
        using var timer = time.CreateTimer(
            _ => callbackCount++,
            null,
            TimeSpan.FromMilliseconds(250),
            Timeout.InfiniteTimeSpan
        );

        time.Advance(TimeSpan.FromMilliseconds(249));
        Assert.Equal(0, callbackCount);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, callbackCount);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void Change_PeriodicTimer_UsesReplacementSchedule()
    {
        var time = new MutableTimeProvider(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var callbackCount = 0;
        using var timer = time.CreateTimer(
            _ => callbackCount++,
            null,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20)
        );

        Assert.True(timer.Change(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(75)));

        time.Advance(TimeSpan.FromMilliseconds(199));
        Assert.Equal(0, callbackCount);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, callbackCount);

        time.Advance(TimeSpan.FromMilliseconds(225));
        Assert.Equal(4, callbackCount);
    }

    [Fact]
    public void Dispose_PendingTimer_PreventsCallbackAndChange()
    {
        var time = new MutableTimeProvider(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var callbackCount = 0;
        var timer = time.CreateTimer(
            _ => callbackCount++,
            null,
            TimeSpan.FromMilliseconds(250),
            Timeout.InfiniteTimeSpan
        );

        timer.Dispose();
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, callbackCount);
        Assert.False(timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan));
    }
}
