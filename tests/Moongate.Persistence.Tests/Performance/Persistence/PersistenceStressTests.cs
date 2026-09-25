using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Tests.TestSupport.Persistence.Stress;
using Moongate.Persistence.Tests.TestSupport.Persistence.Stress.Data;
using Xunit.Abstractions;

namespace Moongate.Persistence.Tests.Performance.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceStressTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ITestOutputHelper _output;
    private readonly PostgreSqlFixture _postgres;

    public PersistenceStressTests(ITestOutputHelper output, PostgreSqlFixture postgres)
    {
        _output = output;
        _postgres = postgres;
    }

    [PersistenceStressFact, Trait("Category", "Stress")]
    public async Task VirtualSessions_ConcurrentPersistence_PreservesCommittedData()
    {
        var counts = (Environment.GetEnvironmentVariable("MOONGATE_STRESS_SESSIONS") ?? "100,500,1000")
            .Split(',')
            .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        Assert.All(counts, count => Assert.InRange(count, 1, 10_000));
        var concurrency = ReadInt("MOONGATE_STRESS_CONCURRENCY", 32, 1, 256);
        var seconds = ReadInt("MOONGATE_STRESS_SECONDS", 30, 1, 600);
        var think = ReadInt("MOONGATE_STRESS_THINK_MS", 10, 0, 60_000);
        var path = Environment.GetEnvironmentVariable("MOONGATE_STRESS_REPORT") ??
                   throw new InvalidOperationException("Set MOONGATE_STRESS_REPORT to an output JSON path.");
        var phases = new List<StressPhaseReport>();

        foreach (var sessions in counts)
        {
            await using var database = await _postgres.CreateDatabaseAsync();
            var phase = await PersistenceStressScenario.RunAsync(database, sessions, concurrency, seconds, think);
            phases.Add(phase);
            var json = JsonSerializer.Serialize(
                new
                {
                    RecordedAtUtc = DateTimeOffset.UtcNow,
                    Runtime = RuntimeInformation.FrameworkDescription,
                    OperatingSystem = RuntimeInformation.OSDescription,
                    Environment.ProcessorCount,
                    ConfiguredSecondsPerPhase = seconds,
                    ThinkMilliseconds = think,
                    LatencyBucketMilliseconds = 1,
                    Phases = phases
                },
                JsonOptions
            );
            await File.WriteAllTextAsync(path, json);
            _output.WriteLine(
                $"{sessions} sessions: {phase.SuccessfulOperationsPerSecond:F0} operations/s, verified={phase.Verified}"
            );
            _output.WriteLine(json);
            Assert.Empty(phase.Errors);
            Assert.True(phase.Verified, "Committed data did not match after reopening persistence.");
            Assert.True(
                phase.CommittedTransactions > 0 && phase.RolledBackTransactions > 0,
                "The phase did not exercise commit and rollback. Increase duration or reduce think time."
            );
        }
    }

    private static int ReadInt(string name, int fallback, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(name);
        var parsed = value is null ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
        Assert.InRange(parsed, minimum, maximum);

        return parsed;
    }
}
