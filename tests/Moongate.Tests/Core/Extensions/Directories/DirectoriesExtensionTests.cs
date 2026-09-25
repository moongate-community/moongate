using Moongate.Core.Extensions.Directories;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Core.Extensions.Directories;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class DirectoriesExtensionTests
{
    [Fact]
    public void ResolvePathAndEnvs_CustomVariable_ExpandsBeforeNormalizing()
    {
        using var directory = new TemporaryDirectory();
        var key = $"MOONGATE_TEST_PATH_{Guid.NewGuid():N}";
        using var environment = new EnvironmentVariableScope(key, directory.Path);

        var result = $"${key}/nested".ResolvePathAndEnvs();

        Assert.Equal(Path.Combine(directory.Path, "nested"), result);
    }

    [Theory, InlineData(null), InlineData(""), InlineData(" ")]
    public void ResolvePathAndEnvs_NullOrWhitespace_ReturnsNull(string? path)
    {
        Assert.Null(path!.ResolvePathAndEnvs());
    }

    [Fact]
    public void ResolvePathAndEnvs_TildePath_UsesUserProfileAndReturnsAbsolutePath()
    {
        var expected = Path.GetFullPath(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "moongate"
            )
        );

        Assert.Equal(expected, "~/moongate".ResolvePathAndEnvs());
    }
}
