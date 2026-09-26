using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;
using Tomlyn;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class ProfessionsLoaderTests
{
    private const string Warrior = """
                                   [[profession]]
                                   id = 1
                                   name = "Warrior"
                                   name_cliloc = 1061180
                                   description_cliloc = 1061230
                                   gump = 5577
                                   str = 45
                                   dex = 35
                                   int = 10
                                   skills = [
                                       { skill = "Alchemy", value = 30 },
                                       { skill = "Anatomy", value = 20 },
                                   ]

                                   """;

    private const string Skills = """
                                  [[skill]]
                                  id = "alchemy"
                                  name = "Alchemy"
                                  title = "Alchemist"
                                  profession_name = "Alchemy"
                                  primary_stat = "int"
                                  secondary_stat = "dex"

                                  [[skill]]
                                  id = "anatomy"
                                  name = "Anatomy"
                                  title = "Biologist"
                                  profession_name = "Anatomy"
                                  primary_stat = "str"
                                  secondary_stat = "int"

                                  """;

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsStatsAndSkills()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/professions.toml", Warrior);

        var result = await CreateLoader(root).LoadDataAsync();

        var warrior = Assert.Single(result.Entities);
        Assert.Equal(1, warrior.Id);
        Assert.Equal("Warrior", warrior.Name);
        Assert.Equal(1061180, warrior.NameCliloc);
        Assert.Equal(1061230, warrior.DescriptionCliloc);
        Assert.Equal(5577, warrior.Gump);
        Assert.Equal((45, 35, 10), (warrior.Str, warrior.Dex, warrior.Int));
        Assert.Equal([(SkillType.Alchemy, 30), (SkillType.Anatomy, 20)], warrior.Skills.Select(skill => (skill.Skill, skill.Value)));
    }

    [Theory, InlineData("id = 1", "id = 0"), InlineData("id = 1", "id = -2")]
    public async Task LoadDataAsync_IdBelowOne_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/professions.toml", Warrior.Replace(from, to));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_DuplicateId_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/professions.toml", Warrior + Warrior);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_SkillMissingFromSkillsFile_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/professions.toml", Warrior.Replace("\"Anatomy\"", "\"Tactics\""));

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("Tactics", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadDataAsync_SkillNameThatIsNotASkill_Throws()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/professions.toml", Warrior.Replace("\"Anatomy\"", "\"Juggling\""));

        await Assert.ThrowsAsync<TomlException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_TableWithAnotherName_ThrowsInsteadOfLoadingNothing()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/professions.toml", Warrior.Replace("[[profession]]", "[[professions]]"));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task DataLoaderService_RunsSkillsBeforeProfessions_AndTheLoaderSeesTheSkills()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/skills.toml", Skills);
        root.CreateFile("data/professions.toml", Warrior);
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["data"]));
        container.AddUltimaDataLoader<SkillsLoader, SkillContent>(2);
        container.AddUltimaDataLoader<ProfessionsLoader, ProfessionContent>(3);
        container.Register<IDataLoaderService, DataLoaderService>(Reuse.Singleton);
        var service = container.Resolve<IDataLoaderService>();

        await service.StartAsync();

        Assert.Equal(2, service.GetEntities<SkillContent>().Count);
        Assert.Equal("Warrior", Assert.Single(service.GetEntities<ProfessionContent>()).Name);
    }

    private static ProfessionsLoader CreateLoader(TemporaryDirectory root)
    {
        var skills = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Alchemy, Name = "Alchemy" },
            new SkillContent { Id = SkillType.Anatomy, Name = "Anatomy" }
        );

        return new(new DirectoriesConfig(root.Path, ["data"]), skills);
    }
}
