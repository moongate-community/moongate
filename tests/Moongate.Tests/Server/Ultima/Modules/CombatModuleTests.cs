using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class CombatModuleTests
{
    private readonly RecordingCombatService _combat = new();
    private readonly StubCombatGearService _gear = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0)
    };

    private readonly MobileEntity _orc = new()
        { Id = new Serial(0x100), Name = "an orc", Map = MapType.Trammel, Location = new Point3D(1601, 1600, 0) };

    public CombatModuleTests()
    {
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_orc);
    }

    [Fact]
    public void Attack_MakesTheFirstFightTheSecond()
    {
        Assert.True(Run("return combat.attack(256, 2)")[0].Read<bool>());

        Assert.Equal([(_orc, _aria)], _combat.Attacks);
    }

    [Fact]
    public void Attack_TheServiceRefuses_IsFalse()
    {
        _combat.Allows = false;

        Assert.False(Run("return combat.attack(256, 2)")[0].Read<bool>());
    }

    [Fact]
    public void Weapon_IsFists_WhenTheMobileHoldsNone_AndTheMeleeRangeIsTheServicesOwn()
    {
        _combat.Range = 2;

        var weapon = Run("return combat.weapon(2)")[0].Read<LuaTable>();

        Assert.Equal(("wrestling", false, 2), (weapon["skill"].Read<string>(), weapon["ranged"].Read<bool>(), weapon["range"].Read<int>()));
        Assert.True(weapon["projectile"].Type == LuaValueType.Nil);
    }

    [Theory]
    [InlineData(WeaponType.Bow, "archery", 0x0F42, "arrow")]
    [InlineData(WeaponType.Crossbow, "archery", 0x1BFE, "bolt")]
    public void Weapon_ABowOrACrossbow_TellsWhatItShoots(WeaponType type, string skill, int projectile, string ammo)
    {
        _combat.Weapon = new WeaponInfo(SkillType.Archery, type, true, 9, 41, 25);

        var weapon = Run("return combat.weapon(2)")[0].Read<LuaTable>();

        Assert.Equal(
            (skill, true, projectile, ammo),
            (weapon["skill"].Read<string>(), weapon["ranged"].Read<bool>(), weapon["projectile"].Read<int>(), weapon["ammo"].Read<string>())
        );
    }

    [Fact]
    public void Weapon_AMeleeWeapon_NamesItsSkill()
    {
        _combat.Weapon = new WeaponInfo(SkillType.MaceFighting, WeaponType.Mace, false, 5, 33, 35);

        var weapon = Run("return combat.weapon(2)")[0].Read<LuaTable>();

        Assert.Equal(("mace_fighting", false), (weapon["skill"].Read<string>(), weapon["ranged"].Read<bool>()));
    }

    [Fact]
    public void Swing_PlaysTheSwingTowardsAPlace_AndSpendAmmoAsksTheService()
    {
        Assert.True(Run("return combat.swing(2, 1590, 1600)")[0].Read<bool>());
        Assert.Equal([(_aria, 1590, 1600)], _combat.Swings);

        Assert.True(Run("return combat.spend_ammo(2)")[0].Read<bool>());
        _combat.HasAmmo = false;
        Assert.False(Run("return combat.spend_ammo(2)")[0].Read<bool>());
        Assert.Equal([_aria, _aria], _combat.Spent);
    }

    [Fact]
    public void WeaponSwingAndSpendAmmo_OfAMobileNotInTheWorld_AreNilOrFalse()
    {
        Assert.True(Run("return combat.weapon(999)")[0].Type == LuaValueType.Nil);
        Assert.False(Run("return combat.swing(999, 1, 1)")[0].Read<bool>());
        Assert.False(Run("return combat.spend_ammo(999)")[0].Read<bool>());
    }

    [Theory]
    [InlineData("return combat.attack(999, 2)")]
    [InlineData("return combat.attack(256, 999)")]
    [InlineData("return combat.attack(0, 2)")]
    [InlineData("return combat.attack(256, -1)")]
    public void Attack_AMobileThatIsNotInTheWorld_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_combat.Attacks);
    }

    [Fact]
    public void Stop_EndsTheFight()
    {
        Assert.True(Run("return combat.stop(256)")[0].Read<bool>());

        Assert.Equal([_orc], _combat.Stopped);
        Assert.False(Run("return combat.stop(999)")[0].Read<bool>());
    }

    [Fact]
    public void Range_IsHowFarTheBlowsOfTheMobileReach_OrNil()
    {
        _combat.Range = 10;

        Assert.Equal(10, Run("return combat.range(256)")[0].Read<int>());
        Assert.Equal(LuaValue.Nil, Run("return combat.range(999)")[0]);
    }

    [Fact]
    public void Target_IsTheSerialOfWhomItFights_OrNil()
    {
        _combat.Attack(_orc, _aria);

        Assert.Equal(2L, Run("return combat.target(256)")[0].Read<long>());
        Assert.Equal(LuaValue.Nil, Run("return combat.target(2)")[0]);
        Assert.Equal(LuaValue.Nil, Run("return combat.target(999)")[0]);
    }

    [Fact]
    public void ArmorRating_IsWhatTheGearServiceSays_AndNilForAMobileNotInTheWorld()
    {
        _gear.Armor[ArmorZoneType.Chest] = 30;

        var result = Run("return combat.armor_rating(2), combat.armor_rating(999)");

        Assert.Equal(30, result[0].Read<int>());
        Assert.True(result[1].Type == LuaValueType.Nil);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new CombatModule(_combat, _mobiles, _gear));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
