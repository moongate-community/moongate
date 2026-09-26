using Moongate.Server.Bootstrap.Internal;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class RootDirectoryGuardTests
{
    [Fact]
    public void EnsureNotBinaryDirectory_AnotherDirectorySharingThePrefix_IsAccepted()
    {
        using var directory = new TemporaryDirectory();
        var binary = Path.Combine(directory.Path, "moongate");
        var root = Path.Combine(directory.Path, "moongate-data");

        RootDirectoryGuard.EnsureNotBinaryDirectory(root, binary);
    }

    [Fact]
    public void EnsureNotBinaryDirectory_RootIsTheBinaryDirectory_RefusesNamingTheOption()
    {
        using var directory = new TemporaryDirectory();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RootDirectoryGuard.EnsureNotBinaryDirectory(directory.Path, directory.Path)
        );

        Assert.Contains("--root-directory", exception.Message, StringComparison.Ordinal);
        Assert.Contains(directory.Path, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureNotBinaryDirectory_SameDirectoryWrittenDifferently_IsStillRefused()
    {
        using var directory = new TemporaryDirectory();
        var binary = directory.Path + Path.DirectorySeparatorChar;
        var root = Path.Combine(directory.Path, "elsewhere", "..");

        Assert.Throws<InvalidOperationException>(() => RootDirectoryGuard.EnsureNotBinaryDirectory(root, binary));
    }
}
