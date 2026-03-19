using Moongate.Server.Data.Scripting;
using Moongate.Server.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class DialogueDefinitionServiceTests
{
    [Test]
    public void Register_WhenDefinitionIsValid_ShouldStoreConversation()
    {
        var service = new DialogueDefinitionService();
        var script = new Script();
        var definition = BuildDefinition(script);

        var registered = service.Register("innkeeper", definition, "scripts/dialogues/innkeeper.lua");
        var resolved = service.TryGet("innkeeper", out var conversation);

        Assert.Multiple(
            () =>
            {
                Assert.That(registered, Is.True);
                Assert.That(resolved, Is.True);
                Assert.That(conversation, Is.Not.Null);
                Assert.That(conversation!.ConversationId, Is.EqualTo("innkeeper"));
                Assert.That(conversation.StartNodeId, Is.EqualTo("start"));
                Assert.That(conversation.ScriptPath, Is.EqualTo("scripts/dialogues/innkeeper.lua"));
                Assert.That(conversation.Topics["room"], Is.EqualTo(new[] { "room", "stanza" }));
                Assert.That(conversation.TopicRoutes["room"], Is.EqualTo("room_offer"));
                Assert.That(conversation.Nodes.Keys, Is.EquivalentTo(new[] { "start", "room_offer", "bye" }));
                Assert.That(conversation.Nodes["start"].Options, Has.Count.EqualTo(2));
                Assert.That(conversation.Nodes["room_offer"].Options[0].Condition, Is.Not.Null);
                Assert.That(conversation.Nodes["room_offer"].Options[0].Effects, Is.Not.Null);
            }
        );
    }

    [Test]
    public void Register_WhenStartIsMissing_ShouldThrow()
    {
        var service = new DialogueDefinitionService();
        var script = new Script();
        var definition = BuildDefinition(script);
        definition["start"] = DynValue.Nil;

        Assert.That(
            () => service.Register("innkeeper", definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("missing 'start'")
        );
    }

    [Test]
    public void Register_WhenOptionHasNoGotoOrAction_ShouldThrow()
    {
        var service = new DialogueDefinitionService();
        var script = new Script();
        var definition = BuildDefinition(script);
        var option = definition.Get("nodes").Table!.Get("start").Table!.Get("options").Table!.Get(1).Table!;
        option["goto"] = DynValue.Nil;

        Assert.That(
            () => service.Register("innkeeper", definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("must define either 'goto' or 'action'")
        );
    }

    [Test]
    public void Register_WhenGotoReferencesMissingNode_ShouldThrow()
    {
        var service = new DialogueDefinitionService();
        var script = new Script();
        var definition = BuildDefinition(script);
        var option = definition.Get("nodes").Table!.Get("start").Table!.Get("options").Table!.Get(1).Table!;
        option["goto"] = "missing_node";

        Assert.That(
            () => service.Register("innkeeper", definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("references missing goto node")
        );
    }

    private static Table BuildDefinition(Script script)
    {
        var definition = new Table(script)
        {
            ["start"] = "start"
        };

        definition["topics"] = new Table(script)
        {
            ["room"] = new Table(script)
            {
                [1] = "room",
                [2] = "stanza"
            }
        };

        definition["topic_routes"] = new Table(script)
        {
            ["room"] = "room_offer"
        };

        var acceptCondition = script.DoString("return function(ctx) return ctx ~= nil end").Function;
        var acceptEffects = script.DoString("return function(ctx) return ctx ~= nil end").Function;

        definition["nodes"] = new Table(script)
        {
            ["start"] = new Table(script)
            {
                ["text"] = "Benvenuto.",
                ["options"] = new Table(script)
                {
                    [1] = new Table(script)
                    {
                        ["text"] = "Una stanza",
                        ["goto"] = "room_offer"
                    },
                    [2] = new Table(script)
                    {
                        ["text"] = "Apri il vendor",
                        ["action"] = "open_vendor"
                    }
                }
            },
            ["room_offer"] = new Table(script)
            {
                ["text"] = "Una stanza costa 15 monete d'oro.",
                ["options"] = new Table(script)
                {
                    [1] = new Table(script)
                    {
                        ["text"] = "Accetto",
                        ["condition"] = acceptCondition,
                        ["effects"] = acceptEffects,
                        ["goto"] = "bye"
                    }
                }
            },
            ["bye"] = new Table(script)
            {
                ["text"] = "Buona permanenza.",
                ["options"] = new Table(script)
            }
        };

        return definition;
    }
}
