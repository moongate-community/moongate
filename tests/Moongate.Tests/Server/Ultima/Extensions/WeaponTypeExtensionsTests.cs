using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class WeaponTypeExtensionsTests
{
    [Theory]
    [InlineData(WeaponType.Sword, SkillType.Swordsmanship, false)]
    [InlineData(WeaponType.Axe, SkillType.Swordsmanship, false)]
    [InlineData(WeaponType.PoleArm, SkillType.Swordsmanship, false)]
    [InlineData(WeaponType.Mace, SkillType.MaceFighting, false)]
    [InlineData(WeaponType.Fencing, SkillType.Fencing, false)]
    [InlineData(WeaponType.Bow, SkillType.Archery, true)]
    [InlineData(WeaponType.Crossbow, SkillType.Archery, true)]
    [InlineData(WeaponType.Thrown, SkillType.Throwing, true)]
    public void Skill_IsTheOneUox3FightsTheWeaponWith_AndRangedOnesAreFlagged(WeaponType type, SkillType skill, bool ranged)
    {
        Assert.Equal((skill, ranged), (type.Skill, type.IsRanged));
    }
}
