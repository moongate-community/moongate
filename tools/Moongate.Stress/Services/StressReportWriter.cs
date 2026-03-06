using System.Text.Json;
using Moongate.Stress.Data;

namespace Moongate.Stress.Services;

public sealed class StressReportWriter
{
    public async Task WriteAsync(StressEvaluationResult result, CancellationToken cancellationToken = default)
    {
        PrintSummary(result);

        var directory = Path.Combine("artifacts", "stress");
        Directory.CreateDirectory(directory);

        var outputPath = Path.Combine(directory, "latest.json");

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, result, cancellationToken: cancellationToken);

        Console.WriteLine($"Report written to {outputPath}");
    }

    private static void PrintSummary(StressEvaluationResult result)
    {
        var metrics = result.Metrics;

        Console.WriteLine();
        Console.WriteLine("=== Moongate Stress Summary ===");
        Console.WriteLine($"Clients:               {metrics.TotalClients}");
        Console.WriteLine($"Login succeeded:       {metrics.LoginSucceeded}");
        Console.WriteLine($"Login failed:          {metrics.LoginFailed}");
        Console.WriteLine($"Unexpected disconnect: {metrics.UnexpectedDisconnects}");
        Console.WriteLine($"Moves sent:            {metrics.MovesSent}");
        Console.WriteLine($"Moves acked:           {metrics.MovesAcked}");
        Console.WriteLine(
            $"ACK p50/p95/p99 (ms):  {metrics.AckLatencyP50Ms:F2}/{metrics.AckLatencyP95Ms:F2}/{metrics.AckLatencyP99Ms:F2}"
        );
        Console.WriteLine($"Duration (s):          {metrics.DurationSeconds}");
        Console.WriteLine($"PASSED:                {result.Passed}");

        if (result.FailedConditions.Count > 0)
        {
            Console.WriteLine("Failed conditions:");

            foreach (var failure in result.FailedConditions)
            {
                Console.WriteLine($" - {failure}");
            }
        }

        Console.WriteLine("===============================");
        Console.WriteLine();
    }
}
