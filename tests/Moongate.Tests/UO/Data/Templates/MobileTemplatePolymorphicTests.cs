using System.Text.Json;
using Moongate.Core.Json;
using Moongate.UO.Data.Json.Context;
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
            }
        );
    }

    [Test]
    public void Deserialize_WithPolymorphicTypeMobile_ShouldCreateMobileTemplateDefinition()
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
                       "body": "0x11",
                       "skinHue": "hue(779:790)",
                       "hairHue": "hue(1100:1120)",
                       "hairStyle": 0,
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
                       "fixedEquipment": [],
                       "randomEquipment": []
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
                var mobile = (MobileTemplateDefinition)result[0];
                Assert.That(mobile.Id, Is.EqualTo("orc_warrior"));
                Assert.That(mobile.Body, Is.EqualTo(0x11));
                Assert.That(mobile.SkinHue.IsRange, Is.True);
                Assert.That(mobile.HairHue.IsRange, Is.True);
                Assert.That(mobile.Notoriety, Is.EqualTo(Notoriety.Murdered));
                Assert.That(mobile.GoldDrop.IsDiceExpression, Is.True);
                Assert.That(mobile.LootTables, Is.EquivalentTo(new[] { "bonearmor", "randomgems" }));
                Assert.That(mobile.Sounds[MobileSoundType.StartAttack], Is.EqualTo(0x1AB));
                Assert.That(mobile.Sounds[MobileSoundType.Die], Is.EqualTo(0x1AF));
                Assert.That(mobile.Skills["wrestling"], Is.EqualTo(425));
                Assert.That(mobile.MinDamage, Is.EqualTo(4));
                Assert.That(mobile.MaxDamage, Is.EqualTo(10));
                Assert.That(mobile.Brain, Is.EqualTo("aggressive_orc"));
            }
        );
    }
}
