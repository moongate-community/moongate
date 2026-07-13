using Moongate.Server.Loaders;
using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class SkillLoaderTests
{
    private static string NewDataRoot()
        => Path.Combine(Path.GetTempPath(), "mg-skillloader-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenFileMissing_SeedsDefaultAndRegisters()
    {
        var root = NewDataRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var skills = new SkillService();
        var loader = new SkillLoader(skills, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "skills.yaml")));
            Assert.Equal(58, skills.Count);
            Assert.Equal("Alchemy", skills.GetById(0)!.Name);
            Assert.Equal(Stat.Dex, skills.GetByName("Alchemy")!.SecondaryStat);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFilePresent_LoadsExistingWithoutReseeding()
    {
        var root = NewDataRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var dataDir = directories.RegisterDirectory("data");
        File.WriteAllText(
            Path.Combine(dataDir, "skills.yaml"),
            "- Id: 0\n  Name: Alchemy\n  PrimaryStat: Int\n  SecondaryStat: Dex\n" +
            "- Id: 1\n  Name: Anatomy\n  PrimaryStat: Int\n  SecondaryStat: Str\n"
        );
        var skills = new SkillService();
        var loader = new SkillLoader(skills, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(2, skills.Count);
            Assert.Equal(Stat.Str, skills.GetByName("Anatomy")!.SecondaryStat);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
