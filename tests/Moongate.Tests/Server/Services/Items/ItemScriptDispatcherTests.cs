using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Services.Items;
using Moongate.Server.Types.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public class ItemScriptDispatcherTests
{
    private sealed class ItemScriptDispatcherTestItemService : IItemService
    {
        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    [Test]
    public async Task DispatchAsync_ShouldCallNormalizedLuaFunction_WhenScriptAndHookAreValid()
    {
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
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
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
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
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
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
