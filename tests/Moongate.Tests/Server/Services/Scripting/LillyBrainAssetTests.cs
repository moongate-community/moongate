using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class LillyBrainAssetTests
{
    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

    [Test]
    public void NpcsHumansTemplate_ShouldAssignDedicatedLillyBrain()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "npcs_humans.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var lilly = document.RootElement
                            .EnumerateArray()
                            .First(element => string.Equals(element.GetProperty("id").GetString(), "lilly", StringComparison.Ordinal));

        Assert.That(lilly.GetProperty("brain").GetString(), Is.EqualTo("lilly"));
    }

    [Test]
    public void InitScript_ShouldRequireLillyBrainScript()
    {
        var repositoryRoot = GetRepositoryRoot();
        var initScriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "init.lua");
        var initScript = File.ReadAllText(initScriptPath);

        Assert.That(initScript, Does.Contain("require(\"ai.lilly\")"));
    }

    [Test]
    public void LillyBrainScript_ShouldUseSnakeCaseSpeakerNameProperty()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "lilly.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("speaker.name"));
                Assert.That(script, Does.Contain("ai_dialogue.init(npc, \"lilly.txt\")"));
                Assert.That(script, Does.Contain("ai_dialogue.listener(npc, speaker, text)"));
                Assert.That(script, Does.Contain("ai_dialogue.idle(npc)"));
            }
        );
    }

    [Test]
    public void LillyPromptFile_ShouldExist()
    {
        var repositoryRoot = GetRepositoryRoot();
        var promptPath = Path.Combine(repositoryRoot, "moongate_data", "templates", "npc_ai_prompts", "lilly.txt");

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(promptPath), Is.True);
                Assert.That(File.ReadAllText(promptPath), Does.Contain("You are Lilly"));
            }
        );
    }

    [Test]
    public void LillyBrainScript_ShouldEnsureAiDialogueIsInitializedBeforeListenerAndIdle()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "lilly.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("local function ensure_ai_ready(npc)"));
                Assert.That(script, Does.Contain("if not ensure_ai_ready(npc)"));
                Assert.That(script, Does.Contain("if ensure_ai_ready(npc) then"));
            }
        );
    }
}
