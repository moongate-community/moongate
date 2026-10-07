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

    [Fact]
    public void GetBuildTime_ReadsTheTimeTheAssemblyWasBuiltAt_InUtc()
    {
        var assembly = Built("2026-10-05T14:32:07Z", "Release");

        Assert.Equal(new DateTimeOffset(2026, 10, 5, 14, 32, 7, TimeSpan.Zero), VersionUtils.GetBuildTime(assembly));
    }

    [Theory, InlineData(null), InlineData(""), InlineData("yesterday")]
    public void GetBuildTime_WithoutATimeThatCanBeRead_IsNull(string? written)
    {
        Assert.Null(VersionUtils.GetBuildTime(Built(written, "Release")));
    }

    [Theory, InlineData("Debug"), InlineData("Release")]
    public void GetBuildConfiguration_ReadsHowTheAssemblyWasBuilt(string configuration)
    {
        Assert.Equal(configuration, VersionUtils.GetBuildConfiguration(Built("2026-10-05T14:32:07Z", configuration)));
    }

    [Fact]
    public void GetBuildConfiguration_WithoutTheMetadata_IsEmpty()
    {
        Assert.Equal("", VersionUtils.GetBuildConfiguration(DynamicAssemblyFactory.Create(new(1, 0, 0, 0))));
    }

    [Fact]
    public void FormatBuildTime_IsTheDayAndTheMinuteInUtc_WhateverTheCultureAndTheOffset()
    {
        // 16:32 at +02:00 is 14:32 in UTC.
        var time = new DateTimeOffset(2026, 10, 5, 16, 32, 7, TimeSpan.FromHours(2));

        Assert.Equal("2026-10-05 14:32 UTC", VersionUtils.FormatBuildTime(time));
        Assert.Equal("unknown", VersionUtils.FormatBuildTime(null));
    }

    [Fact]
    public void FormatHeader_FillsTheVersionTheCodenameTheConfigurationAndTheBuildTime()
    {
        var assembly = DynamicAssemblyFactory.Create(
            new(1, 0, 0, 0),
            "0.14.0+abc",
            "Lilly",
            new Dictionary<string, string> { ["BuildTime"] = "2026-10-05T14:32:07Z", ["BuildConfiguration"] = "Release" }
        );

        var header = VersionUtils.FormatHeader(
            "Version: {Version} ({Configuration}) Codename: \"{Codename}\"\nBuilt: {BuildTime}",
            assembly
        );

        Assert.Equal("Version: 0.14.0 (Release) Codename: \"Lilly\"\nBuilt: 2026-10-05 14:32 UTC", header);
    }

    // An assembly built without the metadata, such as one of a plugin: the header still reads well.
    [Fact]
    public void FormatHeader_WithoutBuildMetadata_SaysUnknown()
    {
        var assembly = DynamicAssemblyFactory.Create(new(1, 0, 0, 0), "0.14.0", "Lilly");

        Assert.Equal(
            "0.14.0 (unknown) built unknown",
            VersionUtils.FormatHeader("{Version} ({Configuration}) built {BuildTime}", assembly)
        );
    }

    [Fact]
    public void TheServer_IsBuiltWithItsBuildTimeAndConfiguration()
    {
        var assembly = typeof(Moongate.Server.Commands.EchoCommand).Assembly;

        var built = VersionUtils.GetBuildTime(assembly);

        Assert.NotNull(built);
        // Written by the build: never in the future, and in UTC. How old the binaries under test are is not its business.
        Assert.True(built.Value <= DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal(TimeSpan.Zero, built.Value.Offset);
        Assert.Contains(VersionUtils.GetBuildConfiguration(assembly), new[] { "Debug", "Release" });
    }

    [Theory, InlineData("Release", "Release"), InlineData("Debug", "Debug"), InlineData("", "unknown")]
    public void FormatBuildConfiguration_IsTheConfiguration_OrUnknown(string configuration, string shown)
    {
        Assert.Equal(shown, VersionUtils.FormatBuildConfiguration(configuration));
    }

    // The two lines under the banner sit in its middle, as the banner is drawn.
    [Fact]
    public void TheHeaderOfTheServer_HasItsVersionAndBuildLinesInTheMiddleOfTheBanner()
    {
        var server = typeof(Moongate.Server.Commands.EchoCommand).Assembly;
        var template = ResourceUtils.GetEmbeddedResourceString(server, "Assets/header.txt");
        var assembly = DynamicAssemblyFactory.Create(
            new(1, 0, 0, 0),
            "0.14.0",
            "Lilly",
            new Dictionary<string, string> { ["BuildTime"] = "2026-10-05T14:32:07Z", ["BuildConfiguration"] = "Release" }
        );

        var lines = VersionUtils.FormatHeader(template, assembly).Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var width = lines.Max(line => line.Length);

        foreach (var line in lines.Where(line => line.Contains("Version:") || line.Contains("Built:")))
        {
            var left = line.Length - line.TrimStart().Length;
            var right = width - line.Length;

            Assert.InRange(left - right, -2, 2);
        }
    }

    private static System.Reflection.Assembly Built(string? time, string configuration)
    {
        var metadata = new Dictionary<string, string> { ["BuildConfiguration"] = configuration };

        if (time is not null)
        {
            metadata["BuildTime"] = time;
        }

        return DynamicAssemblyFactory.Create(new(1, 0, 0, 0), "1.0.0", "Lilly", metadata);
    }
}
