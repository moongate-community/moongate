using Moongate.Stress.Data;
using Moongate.Stress.Interfaces;

namespace Moongate.Stress.Services;

public sealed class StressScenarioRunner
{
    private readonly StressRunOptions _options;
    private readonly IAccountBootstrapper _accountBootstrapper;
    private readonly StressMetricsCollector _metricsCollector;

    public StressScenarioRunner(
        StressRunOptions options,
        IAccountBootstrapper accountBootstrapper,
        StressMetricsCollector metricsCollector
    )
    {
        _options = options;
        _accountBootstrapper = accountBootstrapper;
        _metricsCollector = metricsCollector;
    }

    public async Task<StressEvaluationResult> RunAsync(CancellationToken cancellationToken = default)
    {
        await _accountBootstrapper.EnsureUsersAsync(cancellationToken);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(_options.Duration);

        var tasks = new List<Task>(_options.Clients);

        for (var i = 1; i <= _options.Clients; i++)
        {
            var client = new UoStressClient(i, _options, _metricsCollector);
            tasks.Add(client.RunAsync(runCts.Token));

            var rampDelay = TimeSpan.FromSeconds(1.0 / _options.RampUpPerSecond);
            await Task.Delay(rampDelay, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // expected on duration timeout
        }

        var snapshot = _metricsCollector.CreateSnapshot(_options.Clients, _options.Duration);
        var evaluator = new StressSloEvaluator();

        return evaluator.Evaluate(snapshot);
    }
}
