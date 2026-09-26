using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Reflection;

namespace Moongate.Tests.Core.Utils;

public sealed class VersionUtilsTests
{
    [Fact]
    public void GetCodename_IgnoresMetadataUnderAnotherKey()
    {
        var assembly = DynamicAssemblyFactory.Create(new(1, 0, 0, 0), "1.0.0", "Lilly");

        // The lookup is keyed, so an assembly carrying other AssemblyMetadata entries cannot be mistaken for a match.
        Assert.Equal("Lilly", VersionUtils.GetCodename(assembly));
        Assert.Equal("", VersionUtils.GetCodename(DynamicAssemblyFactory.Create(new(1, 0, 0, 0))));
    }

    [Fact]
    public void GetCodename_MissingMetadataReturnsEmpty()
    {
        var assembly = DynamicAssemblyFactory.Create(new(1, 0, 0, 0), "1.0.0");

        Assert.Equal("", VersionUtils.GetCodename(assembly));
    }

    [Fact]
    public void GetCodename_NullAssembly_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VersionUtils.GetCodename(null!));
    }

    [Fact]
    public void GetCodename_ReturnsTheValueOfTheCodenameMetadata()
    {
        var assembly = DynamicAssemblyFactory.Create(new(1, 0, 0, 0), "1.0.0", "Lilly");

        Assert.Equal("Lilly", VersionUtils.GetCodename(assembly));
    }

    [Fact]
    public void GetVersion_CoreAssembly_ReturnsThreePartSemverWithoutBuildMetadata()
    {
        var version = VersionUtils.GetVersion();

        Assert.DoesNotContain('+', version);
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", version);
    }

    [Fact]
    public void GetVersion_InformationalVersionTakesPrecedenceAndStripsBuildMetadata()
    {
        var assembly = DynamicAssemblyFactory.Create(new(9, 8, 7, 6), "2.4.6-preview.3+build.sha");

        Assert.Equal("2.4.6-preview.3", VersionUtils.GetVersion(assembly));
    }

    [Theory, InlineData(null), InlineData(" ")]
    public void GetVersion_MissingInformationalVersionFallsBackToAssemblyVersion(string? informationalVersion)
    {
        var assembly = DynamicAssemblyFactory.Create(new(3, 2, 1, 0), informationalVersion);

        Assert.Equal("3.2.1.0", VersionUtils.GetVersion(assembly));
    }

    [Fact]
    public void GetVersion_NullAssembly_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VersionUtils.GetVersion(null!));
    }
}
