namespace Moongate.Tests.Server.Services.Scripting;

public sealed class NpcDialogueBridgeAssetTests
{
    [Test]
    public void NpcDialogueBridgeScript_ShouldSupportDialogueThenAiFallback()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "common", "npc_dialogue.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("function npc_dialogue.init"));
                Assert.That(script, Does.Contain("function npc_dialogue.listener"));
                Assert.That(script, Does.Contain("dialogue.listener"));
                Assert.That(script, Does.Contain("ai_dialogue.listener"));
                Assert.That(script, Does.Contain("function npc_dialogue.idle"));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
