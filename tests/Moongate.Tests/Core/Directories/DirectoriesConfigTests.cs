using Moongate.Core.Directories;

namespace Moongate.Tests.Core.Directories;

public sealed class DirectoriesConfigTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public DirectoriesConfigTests()
    {
        _root = Directory.CreateTempSubdirectory("moongate-directories-config-");
    }

    [Fact]
    public void GetPath_WithAForwardSlashSeparatedType_BuildsANestedPathUsingThePlatformSeparator()
    {
        var config = new DirectoriesConfig(_root.FullName, []);

        var path = config.GetPath("templates/test/test");

        // Asserted by segment, never against a hardcoded '/' or '\' literal: the input's
        // separator is the caller's convention, not the host OS's.
        var expected = Path.Combine(_root.FullName, "templates", "test", "test");
        Assert.Equal(expected, path);
        Assert.Equal(["templates", "test", "test"], RelativeSegments(path));
    }

    [Fact]
    public void GetPath_WithABackslashSeparatedType_BuildsTheSameNestedPath()
    {
        var config = new DirectoriesConfig(_root.FullName, []);

        var path = config.GetPath("templates\\test\\test");

        Assert.Equal(Path.Combine(_root.FullName, "templates", "test", "test"), path);
    }

    [Fact]
    public void GetPath_SnakeCasesEverySegmentIndependently()
    {
        var config = new DirectoriesConfig(_root.FullName, []);

        var path = config.GetPath("Templates/TestFolder/AnotherTest");

        Assert.Equal(["templates", "test_folder", "another_test"], RelativeSegments(path));
    }

    [Fact]
    public void GetPath_IgnoresLeadingTrailingAndDoubledSeparators()
    {
        var config = new DirectoriesConfig(_root.FullName, []);

        var path = config.GetPath("/templates//test/");

        Assert.Equal(["templates", "test"], RelativeSegments(path));
    }

    [Fact]
    public void GetPath_CreatesEveryMissingIntermediateDirectory()
    {
        var config = new DirectoriesConfig(_root.FullName, []);

        var path = config.GetPath("templates/test/test");

        Assert.True(Directory.Exists(path));
        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "templates", "test")));
        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "templates")));
    }

    [Fact]
    public void CreateDirectoryIfNotExists_CreatesANestedDirectory()
    {
        var config = new DirectoriesConfig(_root.FullName, []);

        config.CreateDirectoryIfNotExists("templates/test/test");

        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "templates", "test", "test")));
    }

    private string[] RelativeSegments(string path)
        => Path.GetRelativePath(_root.FullName, path).Split(Path.DirectorySeparatorChar);

    public void Dispose()
        => _root.Delete(true);
}
