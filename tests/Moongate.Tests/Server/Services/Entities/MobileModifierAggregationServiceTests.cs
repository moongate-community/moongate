using Moongate.Server.Services.Entities;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Entities;

public class MobileModifierAggregationServiceTests
{
    [Test]
    public void RecalculateEquipmentModifiers_ShouldIgnoreItemsWithoutModifiers()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001031
        };

        mobile.AddEquippedItem(
            ItemLayerType.Shirt,
            new UOItemEntity
            {
                Id = (Serial)0x40001032
            }
        );

        var service = new MobileModifierAggregationService();

        var modifiers = service.RecalculateEquipmentModifiers(mobile);

        Assert.Multiple(
            () =>
            {
                Assert.That(modifiers.StrengthBonus, Is.EqualTo(0));
                Assert.That(modifiers.Luck, Is.EqualTo(0));
                Assert.That(mobile.EquipmentModifiers, Is.Not.Null);
            }
        );
    }

    [Test]
    public void RecalculateEquipmentModifiers_ShouldSumEquippedItemModifiers()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001030,
            BaseStats = new()
            {
                Strength = 60
            }
        };

        mobile.AddEquippedItem(
            ItemLayerType.Shirt,
            new UOItemEntity
            {
                Id = (Serial)0x40001030,
                Modifiers = new()
                {
                    StrengthBonus = 5,
                    FireResist = 3,
                    Luck = 10
                }
            }
        );
        mobile.AddEquippedItem(
            ItemLayerType.Pants,
            new UOItemEntity
            {
                Id = (Serial)0x40001031,
                Modifiers = new()
                {
                    StrengthBonus = 2,
                    FireResist = 4,
                    Luck = 20
                }
            }
        );

        var service = new MobileModifierAggregationService();

        var modifiers = service.RecalculateEquipmentModifiers(mobile);

        Assert.Multiple(
            () =>
            {
                Assert.That(modifiers.StrengthBonus, Is.EqualTo(7));
                Assert.That(modifiers.FireResist, Is.EqualTo(7));
                Assert.That(modifiers.Luck, Is.EqualTo(30));
                Assert.That(mobile.EquipmentModifiers, Is.Not.Null);
                Assert.That(mobile.EquipmentModifiers!.StrengthBonus, Is.EqualTo(7));
                Assert.That(mobile.EffectiveStrength, Is.EqualTo(67));
            }
        );
    }

    [Test]
    public void RecalculateEquipmentModifiers_ShouldIncludeQuiverDefenseChanceIncrease()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00001050
        };

        mobile.AddEquippedItem(
            ItemLayerType.Cloak,
            new UOItemEntity
            {
                Id = (Serial)0x40001050,
                IsQuiver = true,
                Modifiers = new()
                {
                    DefenseChanceIncrease = 5
                }
            }
        );

        var service = new MobileModifierAggregationService();

        var modifiers = service.RecalculateEquipmentModifiers(mobile);

        Assert.Multiple(
            () =>
            {
                Assert.That(modifiers.DefenseChanceIncrease, Is.EqualTo(5));
                Assert.That(mobile.EffectiveDefenseChanceIncrease, Is.EqualTo(5));
            }
        );
    }
}
