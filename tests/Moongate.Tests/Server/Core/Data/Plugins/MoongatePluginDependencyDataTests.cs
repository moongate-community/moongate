using Moongate.Server.Core.Data.Plugins;

namespace Moongate.Tests.Server.Core.Data.Plugins;

public sealed class MoongatePluginDependencyDataTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsInvalidId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MoongatePluginDependencyData(id!));
    }
}
