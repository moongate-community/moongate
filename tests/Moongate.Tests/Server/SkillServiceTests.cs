using Moongate.Server.Services;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class SkillServiceTests
{
    private static SkillService NewService()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-skills-" + Guid.NewGuid().ToString("N"));
        return new SkillService(new DirectoriesConfig(root, Array.Empty<string>()));
    }

    private static SkillDefinition Def(int id, string name)
    {
        return new SkillDefinition { Id = id, Name = name, PrimaryStat = Stat.Int, SecondaryStat = Stat.Dex };
    }

    [Fact]
    public void Register_ThenLookup_FindsByIdAndName()
    {
        var service = NewService();
        service.Register(Def(0, "Alchemy"));
        service.Register(Def(1, "Anatomy"));

        Assert.Equal(2, service.Count);
        Assert.Equal("Alchemy", service.GetById(0)!.Name);
        Assert.Equal(1, service.GetByName("Anatomy")!.Id);
    }

    [Fact]
    public void GetByName_IsCaseInsensitive()
    {
        var service = NewService();
        service.Register(Def(0, "Alchemy"));

        Assert.Equal(0, service.GetByName("alchemy")!.Id);
    }

    [Fact]
    public void Lookup_UnknownKey_ReturnsNull()
    {
        var service = NewService();

        Assert.Null(service.GetById(999));
        Assert.Null(service.GetByName("Nope"));
    }

    [Fact]
    public void All_IsOrderedById()
    {
        var service = NewService();
        service.Register(Def(2, "Animal Lore"));
        service.Register(Def(0, "Alchemy"));
        service.Register(Def(1, "Anatomy"));

        Assert.Equal(new[] { 0, 1, 2 }, service.All.Select(d => d.Id).ToArray());
    }

    [Fact]
    public void LoadFromFile_RegistersEveryDefinition()
    {
        var service = NewService();
        var yaml =
            "- Id: 0\n  Name: Alchemy\n  PrimaryStat: Int\n  SecondaryStat: Dex\n" +
            "- Id: 1\n  Name: Anatomy\n  PrimaryStat: Int\n  SecondaryStat: Str\n";
        var path = Path.Combine(Path.GetTempPath(), "mg-skills-load-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, yaml);

        try
        {
            service.LoadFromFile(path);

            Assert.Equal(2, service.Count);
            Assert.Equal("Alchemy", service.GetById(0)!.Name);
            Assert.Equal(Stat.Str, service.GetByName("Anatomy")!.SecondaryStat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SeedAndLoad_WhenFileMissing_WritesDefaultAndLoadsIt()
    {
        var service = NewService();
        var dataDir = Path.Combine(Path.GetTempPath(), "mg-skills-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);

        try
        {
            service.SeedAndLoad(dataDir);

            Assert.True(File.Exists(Path.Combine(dataDir, "skills.yaml")));
            Assert.True(service.Count >= 6);
            Assert.Equal("Alchemy", service.GetById(0)!.Name);
        }
        finally
        {
            Directory.Delete(dataDir, true);
        }
    }
}
