using Moongate.Server.Data.Items;
using Moongate.Server.Services.Items;
using Moongate.Server.Types.Items;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Items;

public class ItemScriptDispatcherTests
{
    [Test]
    public async Task DispatchAsync_ShouldCallNormalizedLuaFunction_WhenScriptAndHookAreValid()
    {
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var dispatcher = new ItemScriptDispatcher(scriptEngine);
        var context = new ItemScriptContext(
            null,
            new UOItemEntity
            {
                ScriptId = "items.healing-potion"
            },
            ItemScriptHooks.OnUse
        );

        var dispatched = await dispatcher.DispatchAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(scriptEngine.LastFunctionName, Is.EqualTo("on_item_items_healing_potion_on_use"));
                Assert.That(scriptEngine.LastFunctionArgs, Has.Length.EqualTo(1));
                Assert.That(scriptEngine.LastFunctionArgs![0], Is.EqualTo(context));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_ShouldReturnFalse_WhenScriptIdIsMissing()
    {
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var dispatcher = new ItemScriptDispatcher(scriptEngine);
        var context = new ItemScriptContext(
            null,
            new UOItemEntity
            {
                ScriptId = string.Empty
            },
            ItemScriptHooks.OnUse
        );

        var dispatched = await dispatcher.DispatchAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.False);
                Assert.That(scriptEngine.LastFunctionName, Is.Null);
            }
        );
    }

    [Test]
    public async Task DispatchAsync_ShouldReturnFalse_WhenHookIsMissing()
    {
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var dispatcher = new ItemScriptDispatcher(scriptEngine);
        var context = new ItemScriptContext(
            null,
            new UOItemEntity
            {
                ScriptId = "items.healing_potion"
            },
            string.Empty
        );

        var dispatched = await dispatcher.DispatchAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.False);
                Assert.That(scriptEngine.LastFunctionName, Is.Null);
            }
        );
    }
}
