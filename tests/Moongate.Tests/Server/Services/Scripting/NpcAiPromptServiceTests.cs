using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class NpcAiPromptServiceTests
{
    [Test]
    public void TryLoad_WhenPromptExists_ShouldReturnPrompt()
    {
        using var tempDirectory = new TempDirectory();
        var promptsDirectory = Path.Combine(tempDirectory.Path, "templates", "npc_ai_prompts");
        Directory.CreateDirectory(promptsDirectory);
        File.WriteAllText(Path.Combine(promptsDirectory, "lilly.txt"), "You are Lilly.");
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new NpcAiPromptService(directoriesConfig);

        var success = service.TryLoad("lilly.txt", out var prompt);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.True);
                Assert.That(prompt, Is.EqualTo("You are Lilly."));
            }
        );
    }

    [Test]
    public void TryLoad_WhenPromptAttemptsTraversal_ShouldReject()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new NpcAiPromptService(directoriesConfig);

        var success = service.TryLoad("../escape.txt", out var prompt);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(prompt, Is.EqualTo(string.Empty));
            }
        );
    }
}
