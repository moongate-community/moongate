using System.Text.Json;
using Moongate.Core.Json;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.UO.Data.Templates;

public class MobileTemplatePolymorphicTests
{
    [Test]
    public void Context_ShouldRegister_MobileTemplateRootTypes()
    {
        var context = MoongateUOTemplateJsonContext.Default;

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(MobileTemplateDefinitionBase[])),
                    Is.True
                );
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(MobileTemplateDefinition[])), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(MobileVariantTemplate)), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(MobileAppearanceTemplate)), Is.True);
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(MobileEquipmentEntryTemplate)),
                    Is.True
                );
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(MobileWeightedEquipmentItemTemplate)),
                    Is.True
                );
            }
        );
    }

    [Test]
    public void Deserialize_WithCanonicalVariants_ShouldBindVariantData()
    {
        var json = """
                   [
                     {
                       "type": "mobile",
                       "id": "orc_warrior",
                       "name": "Orc Warrior",
                       "category": "monsters",
                       "description": "A tough orc fighter",
                       "tags": ["orc", "melee"],
                       "strength": 70,
                       "dexterity": 45,
                       "intelligence": 25,
                       "hits": 120,
                       "maxHits": 135,
                       "mana": 25,
                       "stamina": 80,
                       "minDamage": 4,
                       "maxDamage": 10,
                       "armorRating": 22,
                       "fame": 800,
                       "karma": -600,
                       "notoriety": "Murdered",
                       "brain": "aggressive_orc",
                       "sounds": {
                         "StartAttack": 427,
                         "Idle": 428,
                         "Attack": 429,
                         "Defend": 430,
                         "Die": 431
                       },
                       "goldDrop": "dice(1d13+3)",
                       "lootTables": ["bonearmor", "randomgems"],
                       "skills": {
                         "wrestling": 425,
                         "tactics": 425
                       },
                       "tamingDifficulty": 0,
                       "provocationDifficulty": 342,
                       "pacificationDifficulty": 342,
                       "controlSlots": 1,
                       "canRun": true,
                       "fleesAtHitsPercent": -1,
                       "spellAttackType": 8,
                       "spellAttackDelay": 3,
                       "variants": [
                         {
                           "name": "male",
                           "weight": 75,
                           "appearance": {
                             "body": "0x190",
                             "skinHue": "0x0000",
                             "hairHue": "0x44E",
                             "hairStyle": 4,
                             "facialHairHue": "0x04AF",
                             "facialHairStyle": 3
                           },
                           "equipment": [
                             {
                               "layer": "Helm",
                               "itemTemplateId": "orcish_helm",
                               "chance": 0.25,
                               "hue": "0x0481",
                               "params": {
                                 "quality": {
                                   "type": "string",
                                   "value": "exceptional"
                                 }
                               }
                             },
                             {
                               "layer": "OneHanded",
                               "chance": 0.75,
                               "items": [
                                 {
                                   "itemTemplateId": "orcish_mace",
                                   "weight": 3,
                                   "hue": "0x0590",
                                   "params": {
                                     "style": {
                                       "type": "String",
                                       "value": "2"
                                     }
                                   }
                                 }
                               ]
                             }
                           ]
                         }
                       ]
                     }
                   ]
                   """;

        var deserialized = JsonSerializer.Deserialize(
            json,
            MoongateUOTemplateJsonContext.Default.GetTypeInfo(typeof(MobileTemplateDefinitionBase[]))
        );
        var result = deserialized as MobileTemplateDefinitionBase[];

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Length, Is.EqualTo(1));
                Assert.That(result[0], Is.TypeOf<MobileTemplateDefinition>());
            }
        );
        var mobile = (MobileTemplateDefinition)result[0];

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Id, Is.EqualTo("orc_warrior"));
                Assert.That(mobile.Notoriety, Is.EqualTo(Notoriety.Murdered));
                Assert.That(mobile.GoldDrop.IsDiceExpression, Is.True);
                Assert.That(mobile.LootTables, Is.EquivalentTo(new[] { "bonearmor", "randomgems" }));
                Assert.That(mobile.Sounds[MobileSoundType.StartAttack], Is.EqualTo(0x1AB));
                Assert.That(mobile.Sounds[MobileSoundType.Die], Is.EqualTo(0x1AF));
                Assert.That(mobile.Skills["wrestling"], Is.EqualTo(425));
                Assert.That(mobile.MinDamage, Is.EqualTo(4));
                Assert.That(mobile.MaxDamage, Is.EqualTo(10));
                Assert.That(mobile.Brain, Is.EqualTo("aggressive_orc"));
                Assert.That(mobile.Variants, Has.Count.EqualTo(1));
            }
        );
        var variant = mobile.Variants.Single();
        Assert.Multiple(
            () =>
            {
                Assert.That(variant.Name, Is.EqualTo("male"));
                Assert.That(variant.Weight, Is.EqualTo(75));
                Assert.That(variant.Appearance.Body, Is.EqualTo(0x190));
                Assert.That(variant.Appearance.SkinHue!.Value.IsRange, Is.False);
                Assert.That(variant.Appearance.SkinHue!.Value.Min, Is.EqualTo(0x0000));
                Assert.That(variant.Appearance.SkinHue!.Value.Max, Is.EqualTo(0x0000));
                Assert.That(variant.Appearance.HairHue!.Value.IsRange, Is.False);
                Assert.That(variant.Appearance.HairHue!.Value.Min, Is.EqualTo(0x044E));
                Assert.That(variant.Appearance.HairHue!.Value.Max, Is.EqualTo(0x044E));
                Assert.That(variant.Appearance.FacialHairHue!.Value.IsRange, Is.False);
                Assert.That(variant.Appearance.FacialHairHue!.Value.Min, Is.EqualTo(0x04AF));
                Assert.That(variant.Appearance.FacialHairHue!.Value.Max, Is.EqualTo(0x04AF));
                Assert.That(variant.Equipment, Has.Count.EqualTo(2));
            }
        );

        var directEquipment = variant.Equipment[0];
        var weightedEquipment = variant.Equipment[1];

        Assert.Multiple(
            () =>
            {
                Assert.That(directEquipment.Layer, Is.EqualTo(ItemLayerType.Helm));
                Assert.That(directEquipment.ItemTemplateId, Is.EqualTo("orcish_helm"));
                Assert.That(directEquipment.Chance, Is.EqualTo(0.25));
                Assert.That(directEquipment.Hue!.Value.IsRange, Is.False);
                Assert.That(directEquipment.Hue!.Value.Min, Is.EqualTo(0x0481));
                Assert.That(directEquipment.Hue!.Value.Max, Is.EqualTo(0x0481));
                Assert.That(directEquipment.Params["quality"].Type, Is.EqualTo(ItemTemplateParamType.String));
                Assert.That(directEquipment.Params["quality"].Value, Is.EqualTo("exceptional"));

                Assert.That(weightedEquipment.Layer, Is.EqualTo(ItemLayerType.OneHanded));
                Assert.That(weightedEquipment.ItemTemplateId, Is.Null);
                Assert.That(weightedEquipment.Chance, Is.EqualTo(0.75));
                Assert.That(weightedEquipment.Items, Has.Count.EqualTo(1));
                Assert.That(weightedEquipment.Items[0].ItemTemplateId, Is.EqualTo("orcish_mace"));
                Assert.That(weightedEquipment.Items[0].Weight, Is.EqualTo(3));
                Assert.That(weightedEquipment.Items[0].Hue!.Value.IsRange, Is.False);
                Assert.That(weightedEquipment.Items[0].Hue!.Value.Min, Is.EqualTo(0x0590));
                Assert.That(weightedEquipment.Items[0].Hue!.Value.Max, Is.EqualTo(0x0590));
                Assert.That(weightedEquipment.Items[0].Params["style"].Type, Is.EqualTo(ItemTemplateParamType.String));
                Assert.That(weightedEquipment.Items[0].Params["style"].Value, Is.EqualTo("2"));
            }
        );
    }
}
