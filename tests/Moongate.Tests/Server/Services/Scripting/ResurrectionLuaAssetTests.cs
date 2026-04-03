namespace Moongate.Tests.Server.Services.Scripting;

public sealed class ResurrectionLuaAssetTests
{
    private readonly string _repositoryRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..")
    );

    [Test]
    public void ResurrectionBridgeScript_ShouldRequireResurrectionGumpModule()
    {
        var bridgePath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "interaction", "resurrection.lua");

        Assert.That(File.Exists(bridgePath), Is.True, $"Missing resurrection bridge script: {bridgePath}");
        Assert.That(File.ReadAllText(bridgePath), Does.Contain("require(\"gumps.resurrection\")"));
    }

    [Test]
    public void ResurrectionItemInit_ShouldRequireResurrectionShrineScript()
    {
        var itemsInitPath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "items", "init.lua");

        Assert.That(File.Exists(itemsInitPath), Is.True, $"Missing items init script: {itemsInitPath}");
        Assert.That(File.ReadAllText(itemsInitPath), Does.Contain("require(\"items.resurrection_shrine\")"));
    }

    [Test]
    public void InteractionInit_ShouldRequireResurrectionBridgeScript()
    {
        var interactionInitPath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "interaction", "init.lua");

        Assert.That(File.Exists(interactionInitPath), Is.True, $"Missing interaction init script: {interactionInitPath}");
        Assert.That(File.ReadAllText(interactionInitPath), Does.Contain("require(\"interaction.resurrection\")"));
    }
}
