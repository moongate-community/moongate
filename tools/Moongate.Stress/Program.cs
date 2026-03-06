using Moongate.Stress.Data;
using Moongate.Stress.Internal;
using Moongate.Stress.Services;

if (!StressRunOptionsParser.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error ?? "Invalid arguments.");
    Console.WriteLine(StressRunOptionsParser.Usage());

    return 2;
}

Console.WriteLine($"Starting Moongate stress test: clients={options.Clients}, duration={options.Duration.TotalSeconds:F0}s");

using var httpClient = new HttpClient
{
    BaseAddress = options.HttpBaseAddress,
    Timeout = TimeSpan.FromSeconds(30)
};

var bootstrapper = new HttpAccountBootstrapper(httpClient, options);
var metrics = new StressMetricsCollector();
var runner = new StressScenarioRunner(options, bootstrapper, metrics);
var writer = new StressReportWriter();

StressEvaluationResult? result = null;

try
{
    result = await runner.RunAsync();
    await writer.WriteAsync(result);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Stress run failed: {ex.Message}");

    return 1;
}

return result.Passed ? 0 : 1;
