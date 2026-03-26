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
    public void NpcsHumansTemplate_ShouldAssignCanonicalAiBlocksToHumanNpcTemplates()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "npcs_humans.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var baseHuman = document.RootElement
                                .EnumerateArray()
                                .First(
                                    element => string.Equals(
                                        element.GetProperty("id").GetString(),
                                        "base_human_npc",
                                        StringComparison.Ordinal
                                    )
                                );
        var genericNpc = document.RootElement
                                 .EnumerateArray()
                                 .First(
                                     element => string.Equals(
                                         element.GetProperty("id").GetString(),
                                         "generic_npc",
                                         StringComparison.Ordinal
                                     )
                                 );
        var healer = document.RootElement
                             .EnumerateArray()
                             .First(
                                 element => string.Equals(
                                     element.GetProperty("id").GetString(),
                                     "healer_npc",
                                     StringComparison.Ordinal
                                 )
                             );
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
                Assert.That(baseHuman.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("none"));
                Assert.That(baseHuman.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("closest"));
                Assert.That(baseHuman.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(baseHuman.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));

                Assert.That(genericNpc.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("none"));
                Assert.That(genericNpc.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("closest"));
                Assert.That(genericNpc.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(genericNpc.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));

                Assert.That(healer.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("town_healer"));
                Assert.That(healer.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("none"));
                Assert.That(healer.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(healer.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));

                Assert.That(lilly.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("lilly"));
                Assert.That(lilly.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("none"));
                Assert.That(lilly.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(lilly.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
