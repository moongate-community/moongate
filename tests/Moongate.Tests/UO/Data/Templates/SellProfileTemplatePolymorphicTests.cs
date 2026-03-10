using System.Text.Json;
using Moongate.Core.Json;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.SellProfiles;

namespace Moongate.Tests.UO.Data.Templates;

public class SellProfileTemplatePolymorphicTests
{
    [Test]
    public void Context_ShouldRegister_SellProfileTemplateRootTypes()
    {
        var context = MoongateUOTemplateJsonContext.Default;

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(SellProfileTemplateDefinitionBase[])),
                    Is.True
                );
                Assert.That(
                    JsonContextTypeResolver.IsTypeRegistered(context, typeof(SellProfileTemplateDefinition[])),
                    Is.True
                );
            }
        );
    }

    [Test]
    public void Deserialize_WithPolymorphicTypeSellProfile_ShouldCreateDefinition()
    {
        var json = """
                   [
                     {
                       "type": "sell_profile",
                       "id": "vendor.blacksmith",
                       "name": "Blacksmith Vendor",
                       "category": "vendors",
                       "description": "Base blacksmith profile",
                       "vendorItems": [
                         { "itemTemplateId": "longsword", "price": 55, "maxStock": 20, "enabled": true }
                       ],
                       "acceptedItems": [
                         { "itemTemplateId": "ingot_iron", "price": 3, "enabled": true },
                         { "tags": ["ore"], "price": 1, "enabled": true }
                       ]
                     }
                   ]
                   """;

        var deserialized = JsonSerializer.Deserialize(
            json,
            MoongateUOTemplateJsonContext.Default.GetTypeInfo(typeof(SellProfileTemplateDefinitionBase[]))
        );
        var result = deserialized as SellProfileTemplateDefinitionBase[];

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Length, Is.EqualTo(1));
                Assert.That(result[0], Is.TypeOf<SellProfileTemplateDefinition>());

                var profile = (SellProfileTemplateDefinition)result[0];
                Assert.That(profile.Id, Is.EqualTo("vendor.blacksmith"));
                Assert.That(profile.VendorItems, Has.Count.EqualTo(1));
                Assert.That(profile.VendorItems[0].ItemTemplateId, Is.EqualTo("longsword"));
                Assert.That(profile.AcceptedItems, Has.Count.EqualTo(2));
                Assert.That(profile.AcceptedItems[1].Tags, Is.EquivalentTo(new[] { "ore" }));
            }
        );
    }
}
