using Moongate.Server.Core.Interfaces.Config;

namespace Moongate.Tests.TestSupport.Config;

/// <summary>
///     A plugin config section with a sub-table and validation, for the <c>AddConfig</c> tests.
/// </summary>
public sealed class SampleSection : IConfigSection
{
    public string Greeting { get; set; } = "hello";

    public SampleLimits Limits { get; set; } = new();

    public void Validate()
    {
        if (Limits.MaxCount < 1)
        {
            throw new InvalidOperationException("sample.limits.max_count must be at least 1.");
        }
    }
}
