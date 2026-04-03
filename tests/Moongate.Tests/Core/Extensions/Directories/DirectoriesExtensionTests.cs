using Moongate.Core.Extensions.Directories;

namespace Moongate.Tests.Core.Extensions.Directories;

public sealed class DirectoriesExtensionTests
{
    [Test]
    public void ResolvePathAndEnvs_WhenPathIsRelative_ShouldReturnAbsolutePath()
    {
        var relativePath = Path.Combine("moongate_data", "web");

        var resolvedPath = relativePath.ResolvePathAndEnvs();

        Assert.That(Path.IsPathRooted(resolvedPath), Is.True);
        Assert.That(resolvedPath, Is.EqualTo(Path.GetFullPath(relativePath)));
    }
}
