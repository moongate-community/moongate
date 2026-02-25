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
                       "weightmax": 40000,
                       "maxitems": 125,
                       "lodamage": 4,
                       "hidamage": 8,
                       "def": 12,
                       "hp": 35,
                       "spd": 30,
                       "str": 10,
                       "stradd": 2,
                       "dex": 6,
                       "dexadd": 1,
                       "int": 3,
                       "intadd": 0,
                       "ammo": "0x0f3f",
                       "ammofx": "0x1bfe",
                       "maxrange": 8,
                       "baserange": 2,
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
                Assert.That(item.Ammo, Is.EqualTo(0x0F3F));
                Assert.That(item.AmmoFx, Is.EqualTo(0x1BFE));
                Assert.That(item.MaxRange, Is.EqualTo(8));
                Assert.That(item.BaseRange, Is.EqualTo(2));
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
