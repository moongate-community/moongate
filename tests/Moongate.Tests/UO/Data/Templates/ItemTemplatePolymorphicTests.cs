using System.Text.Json;
using Moongate.Core.Json;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.UO.Data.Templates;

public class ItemTemplatePolymorphicTests
{
    [Test]
    public void Context_ShouldRegister_TemplateRootTypes()
    {
        var context = MoongateUOTemplateJsonContext.Default;

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(ItemTemplateDefinitionBase[])),
                    Is.True
                );
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(ItemTemplateDefinition[])), Is.True);
            }
        );
    }

    [Test]
    public void Deserialize_WhenLayerIsPresent_ShouldBindItemLayerType()
    {
        var json = """
                   [
                     {
                       "type": "item",
                       "id": "bascinet",
                       "name": "Bascinet",
                       "category": "Armor",
                       "description": "Plate helm",
                       "itemId": "0x140C",
                       "hue": "0",
                       "goldValue": "0",
                       "scriptId": "none",
                       "layer": "Helm"
                     }
                   ]
                   """;

        var deserialized = JsonSerializer.Deserialize(
            json,
            MoongateUOTemplateJsonContext.Default.GetTypeInfo(typeof(ItemTemplateDefinitionBase[]))
        );
        var result = deserialized as ItemTemplateDefinitionBase[];

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Length, Is.EqualTo(1));
                Assert.That(result[0], Is.TypeOf<ItemTemplateDefinition>());
                Assert.That(((ItemTemplateDefinition)result[0]).Layer, Is.EqualTo(ItemLayerType.Helm));
            }
        );
    }

    [Test]
    public void Deserialize_WithPolymorphicTypeItem_ShouldCreateItemTemplateDefinition()
    {
        var json = """
                   [
                     {
                       "type": "item",
                       "id": "leather_backpack",
                       "name": "Leather Backpack",
                       "category": "Container",
                       "description": "Durable leather backpack",
                       "tags": ["container", "backpack"],
                       "itemId": "0x0E76",
                       "hue": "hue(10:80)",
                       "goldValue": "dice(2d8+12)",
                       "weight": 4,
                       "weightMax": 40000,
                       "maxItems": 125,
                       "lowDamage": 4,
                       "highDamage": 8,
                       "defense": 12,
                       "hitPoints": 35,
                       "speed": 30,
                       "strength": 10,
                       "strengthAdd": 2,
                       "dexterity": 6,
                       "dexterityAdd": 1,
                       "intelligence": 3,
                       "intelligenceAdd": 0,
                       "physicalResist": 10,
                       "fireResist": 8,
                       "coldResist": 6,
                       "poisonResist": 4,
                       "energyResist": 2,
                       "hitChanceIncrease": 12,
                       "defenseChanceIncrease": 7,
                       "damageIncrease": 15,
                       "swingSpeedIncrease": 20,
                       "spellDamageIncrease": 25,
                       "fasterCasting": 2,
                       "fasterCastRecovery": 3,
                       "lowerManaCost": 5,
                       "lowerReagentCost": 10,
                       "luck": 100,
                       "spellChanneling": true,
                       "usesRemaining": 30,
                       "ammo": "0x0f3f",
                       "ammoFx": "0x1bfe",
                       "weaponSkill": "Archery",
                       "maxRange": 8,
                       "baseRange": 2,
                       "dyeable": true,
                       "lootType": "Regular",
                       "stackable": false,
                       "gumpId": null,
                       "scriptId": "",
                       "isMovable": true,
                       "container": ["clothing", "jewelry"]
                     }
                   ]
                   """;

        var deserialized = JsonSerializer.Deserialize(
            json,
            MoongateUOTemplateJsonContext.Default.GetTypeInfo(typeof(ItemTemplateDefinitionBase[]))
        );
        var result = deserialized as ItemTemplateDefinitionBase[];

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Length, Is.EqualTo(1));
                Assert.That(result[0], Is.TypeOf<ItemTemplateDefinition>());
                var item = (ItemTemplateDefinition)result[0];
                Assert.That(item.Id, Is.EqualTo("leather_backpack"));
                Assert.That(item.ItemId, Is.EqualTo("0x0E76"));
                Assert.That(item.Container.Count, Is.EqualTo(2));
                Assert.That(item.LootType, Is.EqualTo(LootType.Regular));
                Assert.That(item.Hue.IsRange, Is.True);
                Assert.That(item.Hue.Min, Is.EqualTo(10));
                Assert.That(item.Hue.Max, Is.EqualTo(80));
                Assert.That(item.GoldValue.IsDiceExpression, Is.True);
                Assert.That(item.GoldValue.DiceExpression, Is.EqualTo("2d8+12"));
                Assert.That(item.WeightMax, Is.EqualTo(40000));
                Assert.That(item.MaxItems, Is.EqualTo(125));
                Assert.That(item.LowDamage, Is.EqualTo(4));
                Assert.That(item.HighDamage, Is.EqualTo(8));
                Assert.That(item.Defense, Is.EqualTo(12));
                Assert.That(item.PhysicalResist, Is.EqualTo(10));
                Assert.That(item.FireResist, Is.EqualTo(8));
                Assert.That(item.ColdResist, Is.EqualTo(6));
                Assert.That(item.PoisonResist, Is.EqualTo(4));
                Assert.That(item.EnergyResist, Is.EqualTo(2));
                Assert.That(item.HitChanceIncrease, Is.EqualTo(12));
                Assert.That(item.DefenseChanceIncrease, Is.EqualTo(7));
                Assert.That(item.DamageIncrease, Is.EqualTo(15));
                Assert.That(item.SwingSpeedIncrease, Is.EqualTo(20));
                Assert.That(item.SpellDamageIncrease, Is.EqualTo(25));
                Assert.That(item.FasterCasting, Is.EqualTo(2));
                Assert.That(item.FasterCastRecovery, Is.EqualTo(3));
                Assert.That(item.LowerManaCost, Is.EqualTo(5));
                Assert.That(item.LowerReagentCost, Is.EqualTo(10));
                Assert.That(item.Luck, Is.EqualTo(100));
                Assert.That(item.SpellChanneling, Is.True);
                Assert.That(item.UsesRemaining, Is.EqualTo(30));
                Assert.That(item.Ammo, Is.EqualTo(0x0F3F));
                Assert.That(item.AmmoFx, Is.EqualTo(0x1BFE));
                Assert.That(item.WeaponSkill, Is.EqualTo(UOSkillName.Archery));
                Assert.That(item.MaxRange, Is.EqualTo(8));
                Assert.That(item.BaseRange, Is.EqualTo(2));
                Assert.That(item.Visibility, Is.EqualTo(AccountType.Regular));
            }
        );
    }

    [Test]
    public void SpecsResolve_WhenHueRangeAndDiceValue_ShouldStayWithinExpectedBounds()
    {
        var item = new ItemTemplateDefinition
        {
            Id = "test",
            Name = "test",
            Category = "test",
            Description = "test",
            ItemId = "0x0001",
            Hue = HueSpec.FromRange(5, 55),
            GoldValue = GoldValueSpec.FromDiceExpression("1d8+8")
        };

        for (var i = 0; i < 50; i++)
        {
            var resolvedHue = item.Hue.Resolve();
            var resolvedGold = item.GoldValue.Resolve();

            Assert.Multiple(
                () =>
                {
                    Assert.That(resolvedHue, Is.InRange(5, 55));
                    Assert.That(resolvedGold, Is.InRange(9, 16));
                }
            );
        }
    }
}
