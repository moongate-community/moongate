using System.Text.Json.Serialization;

namespace Moongate.Benchmarks.Compare;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true),
 JsonSerializable(typeof(List<BenchmarkRunResult>))]
/// <summary>
/// Represents MoongateBenchmarksCompareJsonContext.
/// </summary>
public partial class MoongateBenchmarksCompareJsonContext : JsonSerializerContext;
