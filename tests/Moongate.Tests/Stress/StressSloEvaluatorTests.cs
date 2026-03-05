using Moongate.Stress.Data;
using Moongate.Stress.Services;

namespace Moongate.Tests.Stress;

public sealed class StressSloEvaluatorTests
{
    [Test]
    public void Evaluate_WhenAllConditionsSatisfied_ShouldPass()
    {
        var evaluator = new StressSloEvaluator();
        var snapshot = new StressMetricsSnapshot
        {
            TotalClients = 100,
            LoginSucceeded = 99,
            LoginFailed = 1,
            UnexpectedDisconnects = 0,
            MovesSent = 1000,
            MovesAcked = 1000,
            AckLatencyP95Ms = 120
        };

        var result = evaluator.Evaluate(snapshot);

        Assert.That(result.Passed, Is.True);
        Assert.That(result.FailedConditions, Is.Empty);
    }

    [Test]
    public void Evaluate_WhenAnySloFails_ShouldFail()
    {
        var evaluator = new StressSloEvaluator();
        var snapshot = new StressMetricsSnapshot
        {
            TotalClients = 100,
            LoginSucceeded = 50,
            LoginFailed = 50,
            UnexpectedDisconnects = 2,
            MovesSent = 1000,
            MovesAcked = 0,
            AckLatencyP95Ms = 500
        };

        var result = evaluator.Evaluate(snapshot);

        Assert.That(result.Passed, Is.False);
        Assert.That(result.FailedConditions.Count, Is.GreaterThanOrEqualTo(1));
    }
}
