using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class NpcAiMemoryServiceTests
{
    [Test]
    public void LoadOrCreate_WhenFileDoesNotExist_ShouldCreateDefaultMemoryFile()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new NpcAiMemoryService(directoriesConfig);
        var serial = (Serial)0x12314u;

        var memory = service.LoadOrCreate(serial, "Lilly");
        var expectedPath = Path.Combine(tempDirectory.Path, "runtime", "npc_memories", "0x012314.txt");

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(expectedPath), Is.True);
                Assert.That(memory, Does.Contain("[Core Memory]"));
                Assert.That(memory, Does.Contain("Lilly"));
            }
        );
    }

    [Test]
    public void Save_ShouldOverwriteExistingMemorySummary()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new NpcAiMemoryService(directoriesConfig);
        var serial = (Serial)0x12314u;
        var updatedMemory = """
                            [Core Memory]
                            Lilly remembers Marcus.
                            """;

        service.LoadOrCreate(serial, "Lilly");
        service.Save(serial, updatedMemory);
        var loaded = service.LoadOrCreate(serial, "Lilly");

        Assert.That(loaded, Is.EqualTo(updatedMemory));
    }
}
