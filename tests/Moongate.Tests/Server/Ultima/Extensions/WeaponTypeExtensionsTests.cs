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

    [Theory]
    [InlineData(WeaponType.Bow, 10, 0x0F42)]
    [InlineData(WeaponType.Crossbow, 8, 0x1BFE)]
    public void ABow_AndACrossbow_ShootFromTheirRange_WithTheirProjectile(WeaponType type, int range, int projectile)
    {
        Assert.Equal((range, projectile), (type.Range, type.Projectile));
    }

    [Theory]
    [InlineData(WeaponType.Bow, 0x0F3F)]
    [InlineData(WeaponType.Crossbow, 0x1BFB)]
    [InlineData(WeaponType.Sword, 0)]
    public void ABow_ShootsArrows_ACrossbowBolts_AnyOtherWeaponNoAmmo(WeaponType type, int ammo)
    {
        Assert.Equal(ammo, type.Ammo);
    }

    [Theory]
    [InlineData(WeaponType.Sword)]
    [InlineData(WeaponType.Thrown)]
    public void AnyOtherWeapon_ReachesOneCell_AndShootsNothing(WeaponType type)
    {
        Assert.Equal((1, 0), (type.Range, type.Projectile));
    }
}
