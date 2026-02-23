namespace Moongate.Benchmarks.Compare;

public sealed class CompareOptions
{
    public int Iterations { get; private set; } = 200_000;
    public string? OutputPath { get; private set; }
    public bool JsonOutput { get; private set; }

    public static CompareOptions Parse(string[] args)
    {
        var options = new CompareOptions();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--iterations" when i + 1 < args.Length && int.TryParse(args[i + 1], out var iterations):
                    options.Iterations = Math.Max(10_000, iterations);
                    i++;

                    break;
                case "--output" when i + 1 < args.Length:
                    options.OutputPath = args[i + 1];
                    i++;

                    break;
                case "--json":
                    options.JsonOutput = true;

                    break;
            }
        }

        return options;
    }
}
