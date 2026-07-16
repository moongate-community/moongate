using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Skill;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class SkillsTests
{
    [Fact]
    public void GetSkill_ByIndex_ReturnsSkill()
    {
        var (idx, mul) = UltimaFixtures.BuildSkills(("Alchemy", true, 0));
        var dir = UltimaFixtures.CreateClientDirectory(("skills.idx", idx), ("skills.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Skills.Reload();

            var skill = Skills.GetSkill(0);

            Assert.NotNull(skill);
            Assert.Equal(0, skill.Index);
            Assert.Equal("Alchemy", skill.Name);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Reload_SkillFixture_ParsesNameActionAndExtra()
    {
        var (idx, mul) = UltimaFixtures.BuildSkills(("Alchemy", true, 0), ("Anatomy", false, 1));
        var dir = UltimaFixtures.CreateClientDirectory(("skills.idx", idx), ("skills.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Skills.Reload();

            var entries = Skills.SkillEntries;

            Assert.Equal(2, entries.Count);
            Assert.Equal("Alchemy", entries[0].Name);
            Assert.True(entries[0].IsAction);
            Assert.Equal("Anatomy", entries[1].Name);
            Assert.False(entries[1].IsAction);
            Assert.Equal(1, entries[1].Extra);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
