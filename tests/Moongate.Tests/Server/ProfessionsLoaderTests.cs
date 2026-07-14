using Moongate.Server.Loaders;
using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class ProfessionsLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAllProfessions()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var professions = new ProfessionService();
        var loader = new ProfessionsLoader(professions, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "professions.yaml")));
            Assert.Equal(7, professions.Count);
            var mage = professions.GetByName("Mage")!;
            Assert.Equal(4, mage.Skills.Count);
            Assert.Equal(45, mage.Stats.Single(s => s.Type == StatType.Int).Value);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPresent_LoadsExistingWithoutReseeding()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var dataDir = directories.RegisterDirectory("data");
        File.WriteAllText(
            Path.Combine(dataDir, "professions.yaml"),
            "- Name: Tinker\n  Type: Profession\n  Skills:\n  - Name: Tinkering\n    Value: 30\n  Stats:\n  - Type: Dex\n    Value: 45\n"
        );
        var professions = new ProfessionService();
        var loader = new ProfessionsLoader(professions, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, professions.Count);
            Assert.Equal("Tinker", professions.GetByName("Tinker")!.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-prof-" + Guid.NewGuid().ToString("N"));
}
