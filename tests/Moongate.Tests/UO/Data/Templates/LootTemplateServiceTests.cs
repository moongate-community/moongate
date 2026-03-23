using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Loot;

namespace Moongate.Tests.UO.Data.Templates;

public sealed class LootTemplateServiceTests
{
    [Test]
    public void Upsert_WhenDefinitionIsRegistered_ShouldResolveItById()
    {
        var service = new LootTemplateService();
        var definition = new LootTemplateDefinition
        {
            Id = "minor_treasure",
            Name = "Minor Treasure",
            Category = "loot",
            Description = "test loot",
            NoDropWeight = 0,
            Entries =
            [
                new()
                {
                    ItemTemplateId = "gold",
                    Weight = 1,
                    Amount = 10
                }
            ]
        };

        service.Upsert(definition);

        Assert.Multiple(
            () =>
            {
                Assert.That(service.TryGet("minor_treasure", out var resolved), Is.True);
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved!.Entries, Has.Count.EqualTo(1));
                Assert.That(service.Count, Is.EqualTo(1));
            }
        );
    }
}
