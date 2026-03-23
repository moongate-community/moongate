using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public class ItemScriptDispatcherTests
{
    private sealed class ItemScriptDispatcherTestItemService : IItemService
    {
        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    [Test]
    public async Task DispatchAsync_ShouldResolveTableAndInvokeOnClick_ForSingleClick()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            """
            items_healing_potion = {
              on_click = function(ctx)
                _item_dispatch_called = "single"
              end
            }
            """
        );
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
        var context = new ItemScriptContext(
            null,
            new()
            {
                ScriptId = "items.healing-potion"
            },
            "single_click"
        );

        var dispatched = await dispatcher.DispatchAsync(context);
        var result = scriptEngine.ExecuteFunction("(function() return _item_dispatch_called end)()");

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo("single"));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_ShouldResolveTableAndInvokeOnDoubleClick_ForDoubleClick()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            """
            items_healing_potion = {
              on_double_click = function(ctx)
                _item_dispatch_called = "double"
              end
            }
            """
        );
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
        var context = new ItemScriptContext(
            null,
            new()
            {
                ScriptId = "items.healing_potion"
            },
            "double_click"
        );

        var dispatched = await dispatcher.DispatchAsync(context);
        var result = scriptEngine.ExecuteFunction("(function() return _item_dispatch_called end)()");

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo("double"));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_ShouldReturnFalse_WhenHookFunctionIsMissing()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript("items_healing_potion = { }");
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
        var context = new ItemScriptContext(
            null,
            new()
            {
                ScriptId = "items.healing_potion"
            },
            "double_click"
        );

        var dispatched = await dispatcher.DispatchAsync(context);

        Assert.That(dispatched, Is.False);
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
            new()
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
            }
        );
    }

    [Test]
    public async Task DispatchAsync_ShouldReturnFalse_WhenMoonSharpRuntimeIsUnavailable()
    {
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
        var context = new ItemScriptContext(
            null,
            new()
            {
                ScriptId = "items.healing_potion"
            },
            "single_click"
        );

        var dispatched = await dispatcher.DispatchAsync(context);

        Assert.That(dispatched, Is.False);
    }

    [Test]
    public async Task DispatchAsync_WhenScriptIdIsNone_ShouldFallbackToNormalizedItemNameTable()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0"),
            []
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            """
            brick = {
              on_double_click = function(ctx)
                _item_dispatch_called = "brick-double"
              end
            }
            """
        );
        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );
        var context = new ItemScriptContext(
            null,
            new()
            {
                ScriptId = "none",
                Name = "Brick"
            },
            "double_click"
        );

        var dispatched = await dispatcher.DispatchAsync(context);
        var result = scriptEngine.ExecuteFunction("(function() return _item_dispatch_called end)()");

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo("brick-double"));
            }
        );
    }

    [Test]
    public async Task HasHook_ShouldReturnFalse_WhenHookFunctionIsMissing()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript("potion = { }");

        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );

        var hasHook = dispatcher.HasHook(
            new()
            {
                ScriptId = "none",
                Name = "Potion"
            },
            "double_click"
        );

        Assert.That(hasHook, Is.False);
    }

    [Test]
    public async Task HasHook_ShouldReturnTrue_WhenHookFunctionExists()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            """
            potion = {
              on_double_click = function(ctx)
                _ = ctx
              end
            }
            """
        );

        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );

        var hasHook = dispatcher.HasHook(
            new()
            {
                ScriptId = "none",
                Name = "Potion"
            },
            "double_click"
        );

        Assert.That(hasHook, Is.True);
    }

    [Test]
    public async Task HasHook_WhenSharedBeverageScriptIsLoaded_ShouldReturnTrue()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            await File.ReadAllTextAsync(
                Path.GetFullPath(
                    Path.Combine(
                        TestContext.CurrentContext.TestDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "..",
                        "moongate_data",
                        "scripts",
                        "items",
                        "beverage.lua"
                    )
                )
            )
        );

        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );

        var hasHook = dispatcher.HasHook(
            new()
            {
                ScriptId = "items.beverage",
                Name = "Bottle Of Ale"
            },
            "double_click"
        );

        Assert.That(hasHook, Is.True);
    }

    [Test]
    public async Task HasHook_WhenSharedFoodScriptIsLoaded_ShouldReturnTrue()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            await File.ReadAllTextAsync(
                Path.GetFullPath(
                    Path.Combine(
                        TestContext.CurrentContext.TestDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "..",
                        "moongate_data",
                        "scripts",
                        "items",
                        "food.lua"
                    )
                )
            )
        );

        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );

        var hasHook = dispatcher.HasHook(
            new()
            {
                ScriptId = "items.food",
                Name = "Apple"
            },
            "double_click"
        );

        Assert.That(hasHook, Is.True);
    }

    [Test]
    public async Task HasHook_WhenSharedLightScriptIsLoaded_ShouldReturnTrue()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        var scriptEngine = new LuaScriptEngineService(
            directories,
            [],
            new Container(),
            new(temp.Path, scriptsDirectory, "0.1.0")
        );
        await scriptEngine.StartAsync();
        scriptEngine.ExecuteScript(
            await File.ReadAllTextAsync(
                Path.GetFullPath(
                    Path.Combine(
                        TestContext.CurrentContext.TestDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "..",
                        "moongate_data",
                        "scripts",
                        "items",
                        "light_source.lua"
                    )
                )
            )
        );

        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ItemScriptDispatcherTestItemService(),
            new FakeGameNetworkSessionService()
        );

        var hasHook = dispatcher.HasHook(
            new()
            {
                ScriptId = "items.light_source",
                Name = "Candle"
            },
            "double_click"
        );

        Assert.That(hasHook, Is.True);
    }
}
