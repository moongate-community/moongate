namespace Moongate.Tests.Server.Services.Scripting;

public sealed class HelpLuaAssetTests
{
    private readonly string _repositoryRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..")
    );

    [Test]
    public void InitLua_ShouldRequireHelpBridgeScript()
    {
        var initLuaPath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "init.lua");

        Assert.That(File.Exists(initLuaPath), Is.True, $"Missing script bootstrap: {initLuaPath}");
        Assert.That(File.ReadAllText(initLuaPath), Does.Contain("require(\"interaction.init\")"));
    }

    [Test]
    public void HelpBridgeScript_ShouldRequireHelpGumpModule()
    {
        var helpLuaPath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "interaction", "help.lua");

        Assert.That(File.Exists(helpLuaPath), Is.True, $"Missing help bridge script: {helpLuaPath}");
        Assert.That(File.ReadAllText(helpLuaPath), Does.Contain("require(\"gumps.help\")"));
    }

    [Test]
    public void HelpGumpScript_ShouldExistUnderGumpsFolder()
    {
        var helpGumpPath = Path.Combine(_repositoryRoot, "moongate_data", "scripts", "gumps", "help.lua");

        Assert.That(File.Exists(helpGumpPath), Is.True, $"Missing help gump script: {helpGumpPath}");
    }
}
