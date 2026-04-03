namespace Moongate.Tests.Server.Services.Scripting;

public sealed class PublicMoongateLuaAssetTests
{
    private readonly string _repositoryRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..")
    );

    [Test]
    public void PublicMoongateItemInit_ShouldRequirePublicMoongateScript()
    {
        var itemsInitPath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "items", "init.lua");

        Assert.That(File.Exists(itemsInitPath), Is.True, $"Missing items init script: {itemsInitPath}");
        Assert.That(File.ReadAllText(itemsInitPath), Does.Contain("require(\"items.public_moongate\")"));
    }
}
