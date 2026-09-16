using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Reflection;

namespace Moongate.Tests.Core.Utils;

public sealed class VersionUtilsTests
{
    [Fact]
    public void GetVersion_InformationalVersionTakesPrecedenceAndStripsBuildMetadata()
    {
        var assembly = DynamicAssemblyFactory.Create(new Version(9, 8, 7, 6), "2.4.6-preview.3+build.sha");

        Assert.Equal("2.4.6-preview.3", VersionUtils.GetVersion(assembly));
    }

    [Theory, InlineData(null), InlineData(" ")]
    public void GetVersion_MissingInformationalVersionFallsBackToAssemblyVersion(string? informationalVersion)
    {
        var assembly = DynamicAssemblyFactory.Create(new Version(3, 2, 1, 0), informationalVersion);

        Assert.Equal("3.2.1.0", VersionUtils.GetVersion(assembly));
    }

    [Fact]
    public void GetVersion_CoreAssembly_ReturnsConfiguredVersionWithoutBuildMetadata()
    {
        var version = VersionUtils.GetVersion();

        Assert.NotEmpty(version);
        Assert.DoesNotContain('+', version);
    }

    [Fact]
    public void GetVersion_NullAssembly_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VersionUtils.GetVersion(null!));
    }
}
