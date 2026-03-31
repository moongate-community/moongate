using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Quests;

[TestFixture]
public sealed class QuestTemplateServiceTests
{
    [Test]
    public void Clear_WhenDefinitionsExist_ShouldRemoveAllDefinitions()
    {
        var service = new QuestTemplateService();
        service.Upsert(CreateDefinition("quest::one"));
        service.Upsert(CreateDefinition("quest::two"));

        service.Clear();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Count, Is.EqualTo(0));
                Assert.That(service.GetAll(), Is.Empty);
            }
        );
    }

    [Test]
    public void GetAll_WhenDefinitionsExist_ShouldReturnDefinitionsOrderedById()
    {
        var service = new QuestTemplateService();
        service.UpsertRange([CreateDefinition("quest::b"), CreateDefinition("quest::a")]);

        var definitions = service.GetAll();

        Assert.Multiple(
            () =>
            {
                Assert.That(definitions, Has.Count.EqualTo(2));
                Assert.That(definitions[0].Id, Is.EqualTo("quest::a"));
                Assert.That(definitions[1].Id, Is.EqualTo("quest::b"));
            }
        );
    }

    [Test]
    public void TryGet_WhenDefinitionExists_ShouldReturnDefinition()
    {
        var service = new QuestTemplateService();
        var definition = CreateDefinition("quest::find-the-herb");
        service.Upsert(definition);

        var found = service.TryGet("quest::find-the-herb", out var resolved);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(resolved, Is.SameAs(definition));
            }
        );
    }

    [Test]
    public void UpsertRange_WhenDefinitionsExist_ShouldStoreAllDefinitions()
    {
        var service = new QuestTemplateService();
        var first = CreateDefinition("quest::a");
        var second = CreateDefinition("quest::b");

        service.UpsertRange([first, second]);

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Count, Is.EqualTo(2));
                Assert.That(service.TryGet("quest::a", out var storedFirst), Is.True);
                Assert.That(storedFirst, Is.SameAs(first));
                Assert.That(service.TryGet("quest::b", out var storedSecond), Is.True);
                Assert.That(storedSecond, Is.SameAs(second));
            }
        );
    }

    private static QuestTemplateDefinition CreateDefinition(string id)
        => new()
        {
            Id = id,
            Name = id,
            Category = "quest",
            Description = "quest description",
            QuestGiverTemplateIds = ["npc::quest_giver"],
            CompletionNpcTemplateIds = ["npc::quest_turn_in"],
            Repeatable = false,
            MaxActivePerCharacter = 1,
            Objectives =
            [
                new()
                {
                    Type = QuestObjectiveType.Kill,
                    MobileTemplateIds = ["mobile::rat"],
                    Amount = 3
                }
            ],
            Rewards =
            [
                new()
                {
                    Gold = 100,
                    Items =
                    [
                        new()
                        {
                            ItemTemplateId = "item::bandage",
                            Amount = 5
                        }
                    ]
                }
            ]
        };
}
