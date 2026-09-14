using Moongate.Core.Types;
using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Core.Utils;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class PlatformUtilsTests
{
    [Fact]
    public void PlatformFlags_MatchOperatingSystemRuntime()
    {
        Assert.Equal(OperatingSystem.IsWindows(), PlatformUtils.IsRunningOnWindows());
        Assert.Equal(OperatingSystem.IsMacOS(), PlatformUtils.IsRunningOnMacOS());
        Assert.Equal(OperatingSystem.IsLinux(), PlatformUtils.IsRunningOnLinux());
    }

    [Fact]
    public void GetCurrentPlatform_MatchesExclusiveRuntimePlatform()
    {
        var expected = OperatingSystem.IsWindows() ? PlatformType.Windows
            : OperatingSystem.IsMacOS() ? PlatformType.Osx
            : OperatingSystem.IsLinux() ? PlatformType.Linux
            : PlatformType.Unknown;

        Assert.Equal(expected, PlatformUtils.GetCurrentPlatform());
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void IsRunningFromDocker_RequiresExactTrueValue(string? value, bool expected)
    {
        using var environment = new EnvironmentVariableScope("MOONGATE_IS_DOCKER", value);

        Assert.Equal(expected, PlatformUtils.IsRunningFromDocker());
    }
}
