using Moongate.Server.Services.EventLoop;

namespace Moongate.Tests.Server.Services.EventLoop;

public class BackgroundJobServiceTests
{
    [Test]
    public async Task EnqueueBackground_ShouldExecuteQueuedJobs()
    {
        using var service = new BackgroundJobService();
        var counter = 0;
        service.Start(workerCount: 1);

        service.EnqueueBackground(() => Interlocked.Increment(ref counter));

        var completed = await WaitUntilAsync(() => Volatile.Read(ref counter) == 1, TimeSpan.FromSeconds(2));
        await service.StopAsync();

        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task ExecutePendingOnGameLoop_ShouldRunPostedActionsOnlyWhenDrained()
    {
        using var service = new BackgroundJobService();
        var counter = 0;
        service.Start(workerCount: 1);
        service.PostToGameLoop(() => Interlocked.Increment(ref counter));
        service.PostToGameLoop(() => Interlocked.Increment(ref counter));

        Assert.That(counter, Is.Zero);

        var executedOne = service.ExecutePendingOnGameLoop(maxActions: 1);
        var executedTwo = service.ExecutePendingOnGameLoop(maxActions: 10);

        await service.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(executedOne, Is.EqualTo(1));
                Assert.That(executedTwo, Is.EqualTo(1));
                Assert.That(counter, Is.EqualTo(2));
            }
        );
    }

    [Test]
    public async Task EnqueueBackground_WhenJobThrows_ShouldContinueProcessingNextJobs()
    {
        using var service = new BackgroundJobService();
        var counter = 0;
        service.Start(workerCount: 1);

        service.EnqueueBackground(
            () =>
            {
                throw new InvalidOperationException("boom");
            }
        );
        service.EnqueueBackground(() => Interlocked.Increment(ref counter));

        var completed = await WaitUntilAsync(() => Volatile.Read(ref counter) == 1, TimeSpan.FromSeconds(2));
        await service.StopAsync();

        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task RunBackgroundAndPostResult_ShouldPostResultToGameLoop()
    {
        using var service = new BackgroundJobService();
        var result = 0;
        service.Start(workerCount: 1);

        service.RunBackgroundAndPostResult(
            () => 42,
            value => result = value
        );

        var callbackQueued = await WaitUntilAsync(
                                 () => service.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        await service.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(callbackQueued, Is.True);
                Assert.That(result, Is.EqualTo(42));
            }
        );
    }

    [Test]
    public async Task RunBackgroundAndPostResultAsync_WhenThrows_ShouldPostErrorToGameLoop()
    {
        using var service = new BackgroundJobService();
        Exception? captured = null;
        service.Start(workerCount: 1);

        service.RunBackgroundAndPostResultAsync<int>(
            async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("boom");
            },
            _ => Assert.Fail("Result callback should not run"),
            ex => captured = ex
        );

        var callbackQueued = await WaitUntilAsync(
                                 () => service.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        await service.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(callbackQueued, Is.True);
                Assert.That(captured, Is.TypeOf<InvalidOperationException>());
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
