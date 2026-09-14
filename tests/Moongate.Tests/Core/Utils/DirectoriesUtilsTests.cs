using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Core.Utils;

public sealed class DirectoriesUtilsTests
{
    [Fact]
    public void GetFiles_DefaultOverload_RecursesAndAppliesMultiplePatterns()
    {
        using var directory = CreateFileTree();

        var files = DirectoriesUtils.GetFiles(directory.Path, "*.txt", "*.json");

        Assert.Equal(3, files.Length);
        Assert.Contains(Path.Combine(directory.Path, "root.txt"), files);
        Assert.Contains(Path.Combine(directory.Path, "nested", "child.txt"), files);
        Assert.Contains(Path.Combine(directory.Path, "nested", "data.json"), files);
    }

    [Fact]
    public void GetFiles_NonRecursiveWithoutPatterns_ReturnsOnlyTopLevelFiles()
    {
        using var directory = CreateFileTree();

        var files = DirectoriesUtils.GetFiles(directory.Path, false);

        Assert.Equal([Path.Combine(directory.Path, "root.txt")], files);
    }

    [Fact]
    public void GetFiles_NullPatterns_ReturnsAllFilesRecursively()
    {
        using var directory = CreateFileTree();

        var files = DirectoriesUtils.GetFiles(directory.Path, true, null!);

        Assert.Equal(4, files.Length);
    }

    [Fact]
    public void GetFiles_MissingDirectoryOrFilePath_ReturnsEmptyArray()
    {
        using var directory = new TemporaryDirectory();
        var file = directory.CreateFile("file.txt");

        Assert.Empty(DirectoriesUtils.GetFiles(null!));
        Assert.Empty(DirectoriesUtils.GetFiles(Path.Combine(directory.Path, "missing")));
        Assert.Empty(DirectoriesUtils.GetFiles(file));
    }

    private static TemporaryDirectory CreateFileTree()
    {
        var directory = new TemporaryDirectory();
        directory.CreateFile("root.txt");
        directory.CreateFile(Path.Combine("nested", "child.txt"));
        directory.CreateFile(Path.Combine("nested", "data.json"));
        directory.CreateFile(Path.Combine("nested", "ignored.bin"));
        return directory;
    }
}
