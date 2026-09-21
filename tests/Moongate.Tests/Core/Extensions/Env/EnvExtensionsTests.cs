using Moongate.Core.Extensions.Env;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Core.Extensions.Env;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class EnvExtensionsTests
{
    [Fact]
    public void ExpandEnvironmentVariables_AfterScopedChange_UsesRestoredValue()
    {
        var key = $"MOONGATE_TEST_RESTORE_{Guid.NewGuid():N}";
        using var original = new EnvironmentVariableScope(key, "original");

        using (new EnvironmentVariableScope(key, "temporary"))
        {
            Assert.Equal("temporary", $"${key}".ExpandEnvironmentVariables());
        }

        Assert.Equal("original", $"${key}".ExpandEnvironmentVariables());
    }

    [Fact]
    public void ExpandEnvironmentVariables_BracesAndPrefixNames_ExpandExactlyOnce()
    {
        var key = $"MOONGATE_TEST_{Guid.NewGuid():N}";
        using var shortName = new EnvironmentVariableScope(key, "short");
        using var longName = new EnvironmentVariableScope(key + "_LONG", "$" + key);

        Assert.Equal(
            $"short/${key}/tail",
            $"${{{key}}}/${key}_LONG/tail".ExpandEnvironmentVariables()
        );
    }

    [Fact]
    public void ExpandEnvironmentVariables_KnownVariable_ReplacesEveryOccurrence()
    {
        var key = $"MOONGATE_TEST_ENV_{Guid.NewGuid():N}";
        using var environment = new EnvironmentVariableScope(key, "resolved-value");

        var result = $"${key}/$${key}".ExpandEnvironmentVariables();

        Assert.Equal("resolved-value/$resolved-value", result);
    }

    [Theory, InlineData(null), InlineData("")]
    public void ExpandEnvironmentVariables_NullOrEmpty_ReturnsInput(string? input)
        => Assert.Equal(input, input!.ExpandEnvironmentVariables());

    [Fact]
    public void ExpandEnvironmentVariables_UnknownVariable_RemainsUnchanged()
    {
        var key = $"MOONGATE_TEST_MISSING_{Guid.NewGuid():N}";

        Assert.Equal($"before-${key}-after", $"before-${key}-after".ExpandEnvironmentVariables());
    }
}
