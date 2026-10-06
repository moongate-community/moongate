using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class WeaponFamiliesTests
{
    [Theory]
    [InlineData(WeaponType.Sword, false, 0x23B, 0x23A, 9)]
    [InlineData(WeaponType.Sword, true, 0x23B, 0x23A, 13)]
    [InlineData(WeaponType.Axe, true, 0x232, 0x23A, 13)]
    [InlineData(WeaponType.Axe, false, 0x232, 0x23A, 9)]
    [InlineData(WeaponType.PoleArm, true, 0x237, 0x238, 13)]
    [InlineData(WeaponType.Mace, false, 0x233, 0x239, 11)]
    [InlineData(WeaponType.Mace, true, 0x233, 0x239, 12)]
    [InlineData(WeaponType.Fencing, false, 0x23B, 0x238, 10)]
    [InlineData(WeaponType.Fencing, true, 0x23B, 0x238, 14)]
    public void TheFamilyOfAWeapon_GivesItsSoundsAndItsSwing(WeaponType type, bool twoHanded, int hit, int miss, int action)
    {
        Assert.Equal((hit, miss, action), (WeaponFamilies.HitSound(type), WeaponFamilies.MissSound(type), (int)WeaponFamilies.Action(type, twoHanded)));
    }

    [Theory]
    [InlineData(WeaponType.Bow, 0x234, 0x238, 18)]
    [InlineData(WeaponType.Crossbow, 0x234, 0x238, 19)]
    public void AShootingWeapon_HasItsSoundsAndItsShot(WeaponType type, int hit, int miss, int action)
    {
        Assert.Equal((hit, miss, action), (WeaponFamilies.HitSound(type), WeaponFamilies.MissSound(type), (int)WeaponFamilies.Action(type, false)));
    }

    [Fact]
    public void AWeaponWithoutAKind_SoundsAndSwingsAsFists()
    {
        Assert.Equal((0x135, 0x239, (int)HumanAnimationType.Punch), (WeaponFamilies.HitSound(null), WeaponFamilies.MissSound(null), (int)WeaponFamilies.Action(null, false)));
    }

    [Theory]
    [InlineData(WeaponType.Thrown)]
    public void AThrownWeapon_IsNotFoughtWith_SoItSwingsAsFists(WeaponType type)
    {
        Assert.Equal(HumanAnimationType.Punch, WeaponFamilies.Action(type, true));
    }
}
