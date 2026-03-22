using System.Text.Json;
using Moongate.Core.Json;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Loot;

namespace Moongate.Tests.UO.Data.Templates;

public class LootTemplatePolymorphicTests
{
    [Test]
    public void Context_ShouldRegister_LootTemplateRootTypes()
    {
        var context = MoongateUOTemplateJsonContext.Default;

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(LootTemplateDefinitionBase[])),
                    Is.True
                );
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(LootTemplateDefinition[])), Is.True);
            }
        );
    }

    [Test]
    public void Deserialize_WithPolymorphicTypeLoot_ShouldCreateLootTemplateDefinition()
    {
        var json = """
                   [
                     {
                       "type": "loot",
                       "id": "bonearmor",
                       "name": "Bone Armor Loot",
                       "category": "loot",
                       "description": "UOX3 converted loot table",
                       "rolls": 3,
                       "noDropWeight": 442,
                       "entries": [
                         {
                           "weight": 31,
                           "itemId": "0x144e",
                           "amount": 1
                         },
                         {
                           "weight": 31,
                           "itemTag": "armor.bone",
                           "amount": 1
                         }
                       ]
                     }
                   ]
                   """;

        var deserialized = JsonSerializer.Deserialize(
            json,
            MoongateUOTemplateJsonContext.Default.GetTypeInfo(typeof(LootTemplateDefinitionBase[]))
        );
        var result = deserialized as LootTemplateDefinitionBase[];

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Length, Is.EqualTo(1));
                Assert.That(result[0], Is.TypeOf<LootTemplateDefinition>());
                var loot = (LootTemplateDefinition)result[0];
                Assert.That(loot.Id, Is.EqualTo("bonearmor"));
                Assert.That(loot.Rolls, Is.EqualTo(3));
                Assert.That(loot.NoDropWeight, Is.EqualTo(442));
                Assert.That(loot.Entries.Count, Is.EqualTo(2));
                Assert.That(loot.Entries[0].ItemId, Is.EqualTo("0x144e"));
                Assert.That(loot.Entries[1].ItemTag, Is.EqualTo("armor.bone"));
            }
        );
    }
}
