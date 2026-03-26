using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class LillyBrainAssetTests
{
    [Test]
    public void InitScript_ShouldRequireLillyBrainScript()
    {
        var repositoryRoot = GetRepositoryRoot();
        var initScriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "init.lua");
        var initScript = File.ReadAllText(initScriptPath);

        Assert.That(initScript, Does.Contain("require(\"ai.init\")"));
    }

    [Test]
    public void LillyBrainScript_ShouldEnsureAiDialogueIsInitializedBeforeListenerAndIdle()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "npcs", "lilly.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("local function ensure_ai_ready(npc)"));
                Assert.That(script, Does.Contain("if not ensure_ai_ready(npc)"));
                Assert.That(script, Does.Contain("if ensure_ai_ready(npc) then"));
                Assert.That(script, Does.Contain("local npc_dialogue = require(\"common.npc_dialogue\")"));
            }
        );
    }

    [Test]
    public void LillyBrainScript_ShouldUseSnakeCaseSpeakerNameProperty()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "npcs", "lilly.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("speaker.name"));
                Assert.That(script, Does.Contain("prompt_file = \"lilly.txt\""));
                Assert.That(script, Does.Contain("npc_dialogue.listener(npc, speaker, text, DIALOGUE_CONFIG)"));
                Assert.That(script, Does.Contain("npc_dialogue.idle(npc, DIALOGUE_CONFIG)"));
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
    public void NpcsHumansTemplate_ShouldAssignLillyCustomBrainWithoutRepeatingInheritedDefaults()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "npcs_humans.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var lilly = document.RootElement
                            .EnumerateArray()
                            .First(
                                element => string.Equals(
                                    element.GetProperty("id").GetString(),
                                    "lilly",
                                    StringComparison.Ordinal
                                )
                            );

        Assert.Multiple(
            () =>
            {
                Assert.That(lilly.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("lilly"));
                Assert.That(lilly.GetProperty("ai").TryGetProperty("fightMode", out _), Is.False);
                Assert.That(lilly.GetProperty("ai").TryGetProperty("rangePerception", out _), Is.False);
                Assert.That(lilly.GetProperty("ai").TryGetProperty("rangeFight", out _), Is.False);
                Assert.That(lilly.TryGetProperty("brain", out _), Is.False);
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
