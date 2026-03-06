using Moongate.Stress.Services;

namespace Moongate.Tests.Stress;

public sealed class StressMetricsCollectorTests
{
    [Test]
    public void CreateSnapshot_ShouldComputePercentilesAndCounters()
    {
        var metrics = new StressMetricsCollector();

        metrics.MarkLoginSucceeded();
        metrics.MarkLoginSucceeded();
        metrics.MarkLoginFailed();
        metrics.MarkUnexpectedDisconnect();

        for (byte seq = 1; seq <= 5; seq++)
        {
            metrics.MarkMoveSent(1, seq);
            Thread.Sleep(1);
            metrics.MarkMoveAcked(1, seq);
        }

        var snapshot = metrics.CreateSnapshot(3, TimeSpan.FromSeconds(10));

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.LoginSucceeded, Is.EqualTo(2));
                Assert.That(snapshot.LoginFailed, Is.EqualTo(1));
                Assert.That(snapshot.UnexpectedDisconnects, Is.EqualTo(1));
                Assert.That(snapshot.MovesSent, Is.EqualTo(5));
                Assert.That(snapshot.MovesAcked, Is.EqualTo(5));
                Assert.That(snapshot.AckLatencyP50Ms, Is.GreaterThanOrEqualTo(0));
                Assert.That(snapshot.AckLatencyP95Ms, Is.GreaterThanOrEqualTo(snapshot.AckLatencyP50Ms));
            }
        );
    }
}
