namespace Moongate.Tests.Server.Services.Scripting;

public sealed class DialogueLuaAssetTests
{
    [Test]
    public void CommonDialogueScript_ShouldExist()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "common", "dialogue.lua");

        Assert.That(File.Exists(scriptPath), Is.True, $"Missing dialogue helper script: {scriptPath}");
    }

    [Test]
    public void CommonDialogueScript_ShouldRegisterConversationAndExposeBuilders()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "common", "dialogue.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("local runtime = dialogue"));
                Assert.That(script, Does.Contain("function M.conversation(id, definition)"));
                Assert.That(script, Does.Contain("runtime.register(id, definition)"));
                Assert.That(script, Does.Contain("function M.node(definition)"));
                Assert.That(script, Does.Contain("function M.option(definition)"));
                Assert.That(script, Does.Contain("definition.goto_"));
            }
        );
    }

    [Test]
    public void InnkeeperDialogueExample_ShouldExist_AndUseMemoryApis()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "dialogs", "innkeeper.lua");

        Assert.That(File.Exists(scriptPath), Is.True, $"Missing dialogue example script: {scriptPath}");

        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("dialogue.conversation(\"innkeeper\""));
                Assert.That(script, Does.Contain("ctx:set_memory_flag(\"has_rented_room\", true)"));
                Assert.That(script, Does.Contain("ctx:add_memory_number(\"rooms_rented\", 1)"));
                Assert.That(script, Does.Contain("goto_ = \"room_offer\""));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
