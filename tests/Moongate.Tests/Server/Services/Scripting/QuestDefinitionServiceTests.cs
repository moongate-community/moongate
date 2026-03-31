using Moongate.Server.Services.Scripting;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class QuestDefinitionServiceTests
{
    [Test]
    public void Register_WhenDefinitionIsValid_ShouldStoreQuestDefinition()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);

        var registered = service.Register(definition, "scripts/quests/new_haven/rat_hunt.lua");
        var resolved = service.TryGet("new_haven.rat_hunt", out var quest);

        Assert.Multiple(
            () =>
            {
                Assert.That(registered, Is.True);
                Assert.That(resolved, Is.True);
                Assert.That(quest, Is.Not.Null);
                Assert.That(quest!.Id, Is.EqualTo("new_haven.rat_hunt"));
                Assert.That(quest.Name, Is.EqualTo("Rat Hunt"));
                Assert.That(quest.Category, Is.EqualTo("starter"));
                Assert.That(quest.Description, Is.EqualTo("Cull the rat infestation near the mill."));
                Assert.That(quest.QuestGiverTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(quest.CompletionNpcTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(quest.Objectives, Has.Count.EqualTo(3));
                Assert.That(quest.Objectives[0].Type, Is.EqualTo(QuestObjectiveType.Kill));
                Assert.That(quest.Objectives[0].MobileTemplateIds, Is.EqualTo(new[] { "sewer_rat", "giant_rat" }));
                Assert.That(quest.Objectives[1].Type, Is.EqualTo(QuestObjectiveType.Collect));
                Assert.That(quest.Objectives[1].ItemTemplateId, Is.EqualTo("rat_tail"));
                Assert.That(quest.Objectives[2].Type, Is.EqualTo(QuestObjectiveType.Deliver));
                Assert.That(quest.Objectives[2].ItemTemplateId, Is.EqualTo("rat_tail"));
                Assert.That(quest.RewardGold, Is.EqualTo(150));
                Assert.That(quest.RewardItems, Has.Count.EqualTo(1));
                Assert.That(quest.RewardItems[0].ItemTemplateId, Is.EqualTo("bandage"));
                Assert.That(quest.RewardItems[0].Amount, Is.EqualTo(10));
                Assert.That(quest.ScriptPath, Is.EqualTo("scripts/quests/new_haven/rat_hunt.lua"));
            }
        );
    }

    [Test]
    public void Register_WhenDefinitionIsValid_ShouldCompileToQuestTemplateDefinition()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);

        _ = service.Register(definition, "scripts/quests/new_haven/rat_hunt.lua");
        _ = service.TryGet("new_haven.rat_hunt", out var quest);
        var template = quest!.Compile();

        Assert.Multiple(
            () =>
            {
                Assert.That(template.Id, Is.EqualTo("new_haven.rat_hunt"));
                Assert.That(template.QuestGiverTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(template.CompletionNpcTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(template.Objectives, Has.Count.EqualTo(3));
                Assert.That(template.Rewards, Has.Count.EqualTo(1));
                Assert.That(template.Rewards[0].Gold, Is.EqualTo(150));
                Assert.That(template.Rewards[0].Items, Has.Count.EqualTo(1));
                Assert.That(template.Rewards[0].Items[0].ItemTemplateId, Is.EqualTo("bandage"));
            }
        );
    }

    [Test]
    public void Register_WhenIdIsMissing_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition["id"] = DynValue.Nil;

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("missing 'id'")
        );
    }

    [Test]
    public void Register_WhenQuestGiversAreMissing_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition["quest_givers"] = new Table(script);

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("requires at least one quest giver")
        );
    }

    [Test]
    public void Register_WhenCompletionNpcsAreMissing_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition["completion_npcs"] = DynValue.Nil;

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("requires at least one completion npc")
        );
    }

    [Test]
    public void Register_WhenDuplicateQuestIdIsRegistered_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var first = BuildValidDefinition(script);
        var duplicate = BuildValidDefinition(script);

        _ = service.Register(first, "scripts/quests/a.lua");

        Assert.That(
            () => service.Register(duplicate, "scripts/quests/b.lua"),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("is already registered")
        );
    }

    [Test]
    public void Register_WhenObjectiveTypeIsUnsupported_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition.Get("objectives").Table!.Get(1).Table!["type"] = "escort";

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("unsupported objective type 'escort'")
        );
    }

    [Test]
    public void Register_WhenKillObjectiveHasNoMobiles_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition.Get("objectives").Table!.Get(1).Table!["mobiles"] = new Table(script);

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("kill objective requires 'mobiles'")
        );
    }

    [Test]
    public void Register_WhenCollectObjectiveHasNoItemTemplateId_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition.Get("objectives").Table!.Get(2).Table!["item_template_id"] = DynValue.Nil;

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("collect objective requires 'item_template_id'")
        );
    }

    [Test]
    public void Register_WhenRewardShapeIsUnsupported_ShouldThrow()
    {
        var service = new QuestDefinitionService();
        var script = new Script();
        var definition = BuildValidDefinition(script);
        definition.Get("rewards").Table!.Get(1).Table!["type"] = "virtue";

        Assert.That(
            () => service.Register(definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("unsupported reward type 'virtue'")
        );
    }

    private static Table BuildValidDefinition(Script script)
    {
        return new Table(script)
        {
            ["id"] = "new_haven.rat_hunt",
            ["name"] = "Rat Hunt",
            ["category"] = "starter",
            ["description"] = "Cull the rat infestation near the mill.",
            ["quest_givers"] = new Table(script)
            {
                [1] = "farmer_npc"
            },
            ["completion_npcs"] = new Table(script)
            {
                [1] = "farmer_npc"
            },
            ["repeatable"] = false,
            ["max_active_per_character"] = 1,
            ["objectives"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["type"] = "kill",
                    ["mobiles"] = new Table(script)
                    {
                        [1] = "sewer_rat",
                        [2] = "giant_rat"
                    },
                    ["amount"] = 10
                },
                [2] = new Table(script)
                {
                    ["type"] = "collect",
                    ["item_template_id"] = "rat_tail",
                    ["amount"] = 10
                },
                [3] = new Table(script)
                {
                    ["type"] = "deliver",
                    ["item_template_id"] = "rat_tail",
                    ["amount"] = 10
                }
            },
            ["rewards"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["type"] = "gold",
                    ["amount"] = 150
                },
                [2] = new Table(script)
                {
                    ["type"] = "item",
                    ["item_template_id"] = "bandage",
                    ["amount"] = 10
                }
            }
        };
    }
}
