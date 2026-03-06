using Moongate.Stress.Data;

namespace Moongate.Stress.Services;

public sealed class StressSloEvaluator
{
    private const double MinimumLoginSuccessRate = 0.99;
    private const double MaximumP95AckMs = 200;

    public StressEvaluationResult Evaluate(StressMetricsSnapshot snapshot)
    {
        var failed = new List<string>();

        var loginSuccessRate = snapshot.TotalClients == 0
                                   ? 0
                                   : (double)snapshot.LoginSucceeded / snapshot.TotalClients;

        if (loginSuccessRate < MinimumLoginSuccessRate)
        {
            failed.Add($"Login success rate {loginSuccessRate:P2} < {MinimumLoginSuccessRate:P0}");
        }

        if (snapshot.UnexpectedDisconnects != 0)
        {
            failed.Add($"Unexpected disconnects {snapshot.UnexpectedDisconnects} != 0");
        }

        if (snapshot.MovesAcked == 0)
        {
            failed.Add("No movement ACKs received.");
        }

        if (snapshot.AckLatencyP95Ms > MaximumP95AckMs)
        {
            failed.Add($"Movement ACK p95 {snapshot.AckLatencyP95Ms:F2}ms > {MaximumP95AckMs:F0}ms");
        }

        return new()
        {
            Metrics = snapshot,
            FailedConditions = failed
        };
    }
}
