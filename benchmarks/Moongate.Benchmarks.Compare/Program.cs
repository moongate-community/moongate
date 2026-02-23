namespace Moongate.Benchmarks.Compare;

public static class Program
{
    public static int Main(string[] args)
    {
        var options = CompareOptions.Parse(args);
        var runner = new BenchmarkCompareRunner();
        var results = runner.Run(options.Iterations);
        BenchmarkCompareRunner.WriteResults(results, options);

        return 0;
    }
}
