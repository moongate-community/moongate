using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class CombatGearServiceTests
{
    private static readonly Serial Aria = new(2);

    private readonly ItemService _items = TestItems.Create(TestSectors.Create());
    private readonly MobileEntity _aria = new() { Id = Aria, Name = "Aria", AccountId = new Serial(0x42), Strength = 100 };
    private readonly CombatGearService _gear;
    private uint _next = 0x40000010;

    public CombatGearServiceTests()
    {
        _gear = new(
            _items,
            new ItemTemplateService(
                new StubDataLoaderService().With(
                    new ItemTemplate
                    {
                        Id = "longsword", ItemId = new Serial(0x0F60), Layer = LayerType.OneHanded, WeaponType = WeaponType.Sword,
                        DamageMin = 5, DamageMax = 33, Speed = 35
                    },
                    new ItemTemplate
                    {
                        Id = "halberd", ItemId = new Serial(0x143E), Layer = LayerType.TwoHanded, TwoHandedWeapon = true,
                        WeaponType = WeaponType.Sword, DamageMin = 5, DamageMax = 49, Speed = 25
                    },
                    new ItemTemplate
                    {
                        Id = "bow", ItemId = new Serial(0x13B2), Layer = LayerType.TwoHanded, TwoHandedWeapon = true,
                        WeaponType = WeaponType.Bow, DamageMin = 9, DamageMax = 41, Speed = 25
                    },
                    new ItemTemplate { Id = "club", ItemId = new Serial(0x13B4), Layer = LayerType.OneHanded, DamageMin = 3, DamageMax = 9, Speed = 40 },
                    new ItemTemplate { Id = "heater", ItemId = new Serial(0x1B76), Layer = LayerType.TwoHanded, ArmorRating = 8 },
                    new ItemTemplate { Id = "plate_tunic", ItemId = new Serial(0x1415), Layer = LayerType.InnerTorso, ArmorRating = 30 },
                    new ItemTemplate { Id = "chain_tunic", ItemId = new Serial(0x13BF), Layer = LayerType.MiddleTorso, ArmorRating = 28 },
                    new ItemTemplate { Id = "plate_helm", ItemId = new Serial(0x1412), Layer = LayerType.Helm, ArmorRating = 40 },
                    new ItemTemplate { Id = "gorget", ItemId = new Serial(0x1413), Layer = LayerType.Neck, ArmorRating = 30 },
                    new ItemTemplate { Id = "gloves", ItemId = new Serial(0x1414), Layer = LayerType.Gloves, ArmorRating = 30 },
                    new ItemTemplate { Id = "arms", ItemId = new Serial(0x1410), Layer = LayerType.Arms, ArmorRating = 30 },
                    new ItemTemplate { Id = "leggings", ItemId = new Serial(0x1411), Layer = LayerType.Pants, ArmorRating = 30 },
                    new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03), Layer = LayerType.OuterTorso },
                    new ItemTemplate { Id = "studded_tunic", ItemId = new Serial(0x1C02), Layer = LayerType.Shirt, ArmorRating = 16 },
                    new ItemTemplate { Id = "plate_skirt", ItemId = new Serial(0x1416), Layer = LayerType.OuterLegs, ArmorRating = 30 }
                )
            )
        );
    }

    [Fact]
    public void WeaponOf_NothingInTheHands_IsNull()
    {
        Assert.Null(_gear.WeaponOf(_aria));
    }

    [Fact]
    public void WeaponOf_AOneHandedWeapon_IsItsSkillItsDamageItsSpeedAndItsHands()
    {
        Wear("longsword");

        var weapon = _gear.WeaponOf(_aria)!;

        Assert.Equal((SkillType.Swordsmanship, WeaponType.Sword, false, 5, 33, 35), (weapon.Skill, weapon.Type, weapon.TwoHanded, weapon.DamageMin, weapon.DamageMax, weapon.Speed));
    }

    [Fact]
    public void WeaponOf_ATwoHandedWeapon_IsMarkedSo()
    {
        Wear("halberd");

        Assert.True(_gear.WeaponOf(_aria)!.TwoHanded);
    }

    [Fact]
    public void WeaponOf_AWeaponWithoutAKind_IsFoughtWithWrestling()
    {
        Wear("club");

        var weapon = _gear.WeaponOf(_aria)!;

        Assert.Equal((SkillType.Wrestling, (WeaponType?)null, 3, 9, 40), (weapon.Skill, weapon.Type, weapon.DamageMin, weapon.DamageMax, weapon.Speed));
    }

    [Fact]
    public void WeaponOf_ABow_IsNotFoughtWithYet()
    {
        Wear("bow");

        Assert.Null(_gear.WeaponOf(_aria));
    }

    [Fact]
    public void WeaponOf_AShieldIsNotAWeapon_AndAWeaponInTheOtherHandIsStillFound()
    {
        Wear("heater");
        Assert.Null(_gear.WeaponOf(_aria));

        Wear("longsword");

        Assert.Equal(SkillType.Swordsmanship, _gear.WeaponOf(_aria)!.Skill);
    }

    [Fact]
    public void WeaponOf_AWeaponOfAnotherMobile_IsNotMine()
    {
        Wear("longsword", new Serial(3));

        Assert.Null(_gear.WeaponOf(_aria));
    }

    [Fact]
    public void ArmorAt_IsTheBestPieceOfTheLayersOfThePart()
    {
        Wear("plate_tunic");
        Wear("chain_tunic");
        Wear("plate_helm");
        Wear("leggings");

        Assert.Equal((30, 40, 30, 0, 0, 0), (
            _gear.ArmorAt(_aria, ArmorZoneType.Chest), _gear.ArmorAt(_aria, ArmorZoneType.Head), _gear.ArmorAt(_aria, ArmorZoneType.Legs),
            _gear.ArmorAt(_aria, ArmorZoneType.Neck), _gear.ArmorAt(_aria, ArmorZoneType.Hands), _gear.ArmorAt(_aria, ArmorZoneType.Arms)));
    }

    [Fact]
    public void ArmorAt_AStuddedTunicOnTheShirtLayer_IsChestArmor_AndASkirtOnTheOuterLegsIsLegArmor()
    {
        Wear("studded_tunic");
        Wear("plate_skirt");

        Assert.Equal((16, 30), (_gear.ArmorAt(_aria, ArmorZoneType.Chest), _gear.ArmorAt(_aria, ArmorZoneType.Legs)));
    }

    [Fact]
    public void ArmorAt_APieceWithoutAnArmorRating_AddsNothing()
    {
        Wear("robe");

        Assert.Equal(0, _gear.ArmorAt(_aria, ArmorZoneType.Chest));
    }

    [Fact]
    public void ArmorRatingOf_IsTheArmorOfEachPartByHowOftenItIsHit()
    {
        foreach (var piece in new[] { "plate_tunic", "plate_helm", "gorget", "gloves", "arms", "leggings" })
        {
            Wear(piece);
        }

        // 30*.35 + 40*.15 + 30*.07 + 30*.07 + 30*.14 + 30*.22 = 10.5 + 6 + 2.1 + 2.1 + 4.2 + 6.6 = 31.5, rounded as ModernUO
        Assert.Equal(32, _gear.ArmorRatingOf(_aria));
    }

    [Fact]
    public void ArmorRatingOf_NoArmor_IsZero()
    {
        Assert.Equal(0, _gear.ArmorRatingOf(_aria));
    }

    [Fact]
    public void WithGear_GivesTheStatusTheDamageOfTheWeaponWithItsBonuses_AndTheArmorRating()
    {
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Tactics, Base = 1000 });
        Wear("longsword");
        Wear("plate_tunic");

        var status = _gear.WithGear(new MobileStatusInfo { Serial = Aria, Name = "Aria" }, _aria);

        // 5 and 33 with tactics 100 (+50%) and then strength 100 (+20%): 5 * 1.8 = 9 and 33 * 1.8 = 59; 10.5 rounds to 11
        Assert.Equal((9, 59, 11), (status.DamageMin, status.DamageMax, status.PhysicalResistance));
    }

    [Fact]
    public void WithGear_WithoutAWeapon_ShowsTheFists()
    {
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Tactics, Base = 1000 });

        var status = _gear.WithGear(new MobileStatusInfo { Serial = Aria, Name = "Aria" }, _aria);

        // 1 and 8 with tactics 100 and strength 100: 1 * 1.8 = 1 and 8 * 1.8 = 14
        Assert.Equal((1, 14, 0), (status.DamageMin, status.DamageMax, status.PhysicalResistance));
    }

    [Fact]
    public void WithGear_AnNpc_IsLeftAsItIs()
    {
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "an orc" };

        var status = _gear.WithGear(new MobileStatusInfo { Serial = orc.Id, Name = "an orc", PhysicalResistance = 7 }, orc);

        Assert.Equal((0, 0, 7), (status.DamageMin, status.DamageMax, status.PhysicalResistance));
    }

    private void Wear(string template, Serial? wearer = null)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = 1, Amount = 1 };
        item.Equip(wearer ?? Aria, LayerOf(template));
        _items.Add([item]);
    }

    private static LayerType LayerOf(string template)
    {
        return template switch
        {
            "longsword" or "club" => LayerType.OneHanded,
            "halberd" or "bow" or "heater" => LayerType.TwoHanded,
            "plate_tunic" => LayerType.InnerTorso,
            "studded_tunic" => LayerType.Shirt,
            "plate_skirt" => LayerType.OuterLegs,
            "chain_tunic" => LayerType.MiddleTorso,
            "plate_helm" => LayerType.Helm,
            "gorget" => LayerType.Neck,
            "gloves" => LayerType.Gloves,
            "arms" => LayerType.Arms,
            "leggings" => LayerType.Pants,
            _ => LayerType.OuterTorso
        };
    }
}
