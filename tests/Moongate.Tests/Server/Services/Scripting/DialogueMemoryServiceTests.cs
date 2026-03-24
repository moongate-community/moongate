using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class DialogueMemoryServiceTests
{
    [Test]
    public void GetOrCreateEntry_ShouldCreateTypedEntry_AndPersistAfterSave()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new DialogueMemoryService(directoriesConfig);
        var npcId = (Serial)0x12314u;
        var otherMobileId = (Serial)0x00000002u;

        var entry = service.GetOrCreateEntry(npcId, otherMobileId);
        entry.Flags["has_rented_room"] = true;
        entry.Numbers["rooms_rented"] = 2;
        entry.Texts["last_service"] = "room";
        entry.LastNode = "room_done";
        entry.LastTopic = "room";
        service.MarkDirty(npcId);
        service.Save(npcId);

        var reloadedService = new DialogueMemoryService(directoriesConfig);
        var reloadedEntry = reloadedService.GetOrCreateEntry(npcId, otherMobileId);

        Assert.Multiple(
            () =>
            {
                Assert.That(reloadedEntry.Flags["has_rented_room"], Is.True);
                Assert.That(reloadedEntry.Numbers["rooms_rented"], Is.EqualTo(2));
                Assert.That(reloadedEntry.Texts["last_service"], Is.EqualTo("room"));
                Assert.That(reloadedEntry.LastNode, Is.EqualTo("room_done"));
                Assert.That(reloadedEntry.LastTopic, Is.EqualTo("room"));
            }
        );
    }

    [Test]
    public void LoadOrCreate_WhenFileDoesNotExist_ShouldCreateDefaultMemoryFile()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new DialogueMemoryService(directoriesConfig);
        var npcId = (Serial)0x12314u;

        var memoryFile = service.LoadOrCreate(npcId);
        var expectedPath = Path.Combine(tempDirectory.Path, "runtime", "dialogue_memory", "0x012314.json");

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(expectedPath), Is.True);
                Assert.That(memoryFile.NpcId, Is.EqualTo(npcId.Value));
                Assert.That(memoryFile.Entries, Is.Empty);
            }
        );
    }

    [Test]
    public void Save_WhenFileIsNotMarkedDirty_ShouldNotThrowAndShouldKeepExistingData()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new DialogueMemoryService(directoriesConfig);
        var npcId = (Serial)0x12314u;

        _ = service.LoadOrCreate(npcId);

        Assert.DoesNotThrow(() => service.Save(npcId));
    }
}
