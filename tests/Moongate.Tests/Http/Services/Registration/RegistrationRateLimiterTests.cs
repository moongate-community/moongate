using Moongate.Http.Plugin.Services.Registration;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Http.Services.Registration;

public sealed class RegistrationRateLimiterTests
{
    private static MutableTimeProvider Time()
        => new(DateTimeOffset.UnixEpoch);

    [Fact]
    public void AllowsUpToLimitThenBlocks()
    {
        var limiter = new RegistrationRateLimiter(Time(), permitPerWindow: 2, window: TimeSpan.FromMinutes(10));

        Assert.True(limiter.TryAcquire("1.1.1.1"));
        Assert.True(limiter.TryAcquire("1.1.1.1"));
        Assert.False(limiter.TryAcquire("1.1.1.1"));
    }

    [Fact]
    public void SeparateKeysHaveSeparateBudgets()
    {
        var limiter = new RegistrationRateLimiter(Time(), 1, TimeSpan.FromMinutes(10));

        Assert.True(limiter.TryAcquire("1.1.1.1"));
        Assert.True(limiter.TryAcquire("2.2.2.2"));
        Assert.False(limiter.TryAcquire("1.1.1.1"));
    }

    [Fact]
    public void WindowSlidesForward()
    {
        var time = Time();
        var limiter = new RegistrationRateLimiter(time, 1, TimeSpan.FromMinutes(10));

        Assert.True(limiter.TryAcquire("1.1.1.1"));
        Assert.False(limiter.TryAcquire("1.1.1.1"));

        time.Advance(TimeSpan.FromMinutes(11));
        Assert.True(limiter.TryAcquire("1.1.1.1"));
    }
}
