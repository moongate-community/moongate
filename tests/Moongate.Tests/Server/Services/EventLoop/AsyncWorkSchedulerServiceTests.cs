using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Services.EventLoop;

namespace Moongate.Tests.Server.Services.EventLoop;

public sealed class AsyncWorkSchedulerServiceTests
{
    [Test]
    public async Task TrySchedule_ShouldRunWorkInBackgroundAndPostResultToGameLoop()
    {
        using var backgroundJobService = new BackgroundJobService();
        backgroundJobService.Start(1);
        IAsyncWorkSchedulerService scheduler = new AsyncWorkSchedulerService(backgroundJobService);
        var result = 0;

        var scheduled = scheduler.TrySchedule(
            "npc-dialogue",
            0x100u,
            _ => Task.FromResult(42),
            value => result = value
        );

        var callbackQueued = await WaitUntilAsync(
                                 () => backgroundJobService.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        await backgroundJobService.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(scheduled, Is.True);
                Assert.That(callbackQueued, Is.True);
                Assert.That(result, Is.EqualTo(42));
            }
        );
    }

    [Test]
    public async Task TrySchedule_WhenBackgroundWorkFails_ShouldPostErrorAndReleaseKey()
    {
        using var backgroundJobService = new BackgroundJobService();
        backgroundJobService.Start(1);
        IAsyncWorkSchedulerService scheduler = new AsyncWorkSchedulerService(backgroundJobService);
        Exception? captured = null;

        var first = scheduler.TrySchedule<int, int>(
            "npc-dialogue",
            0x100,
            _ => throw new InvalidOperationException("boom"),
            _ => Assert.Fail("Result callback should not run"),
            ex => captured = ex
        );

        var errorQueued = await WaitUntilAsync(
                              () => backgroundJobService.ExecutePendingOnGameLoop() > 0,
                              TimeSpan.FromSeconds(2)
                          );

        var second = scheduler.TrySchedule(
            "npc-dialogue",
            0x100,
            _ => Task.FromResult(9),
            _ => { }
        );
        await backgroundJobService.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(errorQueued, Is.True);
                Assert.That(captured, Is.TypeOf<InvalidOperationException>());
                Assert.That(second, Is.True);
            }
        );
    }

    [Test]
    public async Task TrySchedule_WhenSameKeyIsAlreadyInFlight_ShouldRejectDuplicateUntilCallbackRuns()
    {
        using var backgroundJobService = new BackgroundJobService();
        backgroundJobService.Start(1);
        IAsyncWorkSchedulerService scheduler = new AsyncWorkSchedulerService(backgroundJobService);
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackValue = 0;

        var first = scheduler.TrySchedule(
            "npc-dialogue",
            0x100u,
            _ => gate.Task,
            value => callbackValue = value
        );
        var second = scheduler.TrySchedule(
            "npc-dialogue",
            0x100u,
            _ => Task.FromResult(7),
            _ => Assert.Fail("Duplicate callback should not run while first is in flight")
        );

        gate.SetResult(42);

        var callbackQueued = await WaitUntilAsync(
                                 () => backgroundJobService.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );

        var third = scheduler.TrySchedule(
            "npc-dialogue",
            0x100u,
            _ => Task.FromResult(7),
            value => callbackValue = value
        );

        var secondCallbackQueued = await WaitUntilAsync(
                                       () => backgroundJobService.ExecutePendingOnGameLoop() > 0,
                                       TimeSpan.FromSeconds(2)
                                   );
        await backgroundJobService.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
                Assert.That(callbackQueued, Is.True);
                Assert.That(third, Is.True);
                Assert.That(secondCallbackQueued, Is.True);
                Assert.That(callbackValue, Is.EqualTo(7));
            }
        );
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }
}
