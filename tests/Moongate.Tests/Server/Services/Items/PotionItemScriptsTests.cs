using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Data.Luarc;
using Moongate.Scripting.Services;
using Moongate.Server.Modules;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public sealed class PotionItemScriptsTests
{
    private sealed class PotionItemScriptsTestItemService : IItemService
    {
        public UOItemEntity? Item { get; set; }

        public Serial? DeletedItemId { get; private set; }

        public int UpsertCalls { get; private set; }

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedItemId = itemId;

            if (Item?.Id == itemId)
            {
                Item = null;
            }

            return Task.FromResult(true);
        }

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
            => Task.FromResult(Item?.Id == itemId ? Item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((Item is not null && Item.Id == itemId, Item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            Item = item;
            UpsertCalls++;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    private sealed class PotionItemScriptsTestBackgroundJobService : IBackgroundJobService
    {
        private readonly Queue<Func<Task>> _backgroundJobs = new();
        private readonly Queue<Action> _gameLoopActions = new();

        public void EnqueueBackground(Action job)
            => _backgroundJobs.Enqueue(() =>
            {
                job();
                return Task.CompletedTask;
            });

        public void EnqueueBackground(Func<Task> job)
            => _backgroundJobs.Enqueue(job);

        public int ExecutePendingOnGameLoop(int maxActions = 100)
        {
            var executed = 0;

            while (executed < maxActions && _gameLoopActions.Count > 0)
            {
                _gameLoopActions.Dequeue()();
                executed++;
            }

            return executed;
        }

        public void PostToGameLoop(Action action)
            => _gameLoopActions.Enqueue(action);

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void Start(int? workerCount = null)
            => throw new NotSupportedException();

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task DispatchAsync_WhenLesserHealPotionIsUsed_ShouldConsumeOneAndRestoreHits()
    {
        using var environment = await CreateEnvironmentAsync(
            "lesser_heal_potion",
            mobile: new()
            {
                Id = (Serial)0x501u,
                Name = "Potion Tester",
                IsAlive = true,
                Hits = 20,
                MaxHits = 40
            },
            item: new()
            {
                Id = (Serial)0x700u,
                Name = "Lesser Heal Potion",
                ScriptId = "items.lesser_heal_potion",
                ItemId = 0x0F0C,
                Amount = 2,
                MapId = 0,
                Location = Point3D.Zero
            }
        );

        var dispatched = await environment.Dispatcher.DispatchAsync(
            new ItemScriptContext(environment.Session, environment.ItemService.Item!, "double_click")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.ItemService.Item, Is.Not.Null);
                Assert.That(environment.ItemService.Item!.Amount, Is.EqualTo(1));
                Assert.That(environment.Mobile.Hits, Is.EqualTo(30));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_WhenRefreshPotionIsUsed_ShouldDeleteLastPotionAndRestoreStamina()
    {
        using var environment = await CreateEnvironmentAsync(
            "refresh_potion",
            mobile: new()
            {
                Id = (Serial)0x502u,
                Name = "Potion Tester",
                IsAlive = true,
                Stamina = 5,
                MaxStamina = 15
            },
            item: new()
            {
                Id = (Serial)0x701u,
                Name = "Refresh Potion",
                ScriptId = "items.refresh_potion",
                ItemId = 0x0F0B,
                Amount = 1,
                MapId = 0,
                Location = Point3D.Zero
            }
        );

        var dispatched = await environment.Dispatcher.DispatchAsync(
            new ItemScriptContext(environment.Session, environment.ItemService.Item!, "double_click")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.ItemService.Item, Is.Null);
                Assert.That(environment.ItemService.DeletedItemId, Is.EqualTo((Serial)0x701u));
                Assert.That(environment.Mobile.Stamina, Is.EqualTo(15));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_WhenAgilityPotionIsUsed_ShouldApplyTemporaryDexterityBonus()
    {
        using var environment = await CreateEnvironmentAsync(
            "agility_potion",
            mobile: new()
            {
                Id = (Serial)0x503u,
                Name = "Potion Tester",
                IsAlive = true,
                Dexterity = 25,
                Stamina = 25,
                MaxStamina = 25
            },
            item: new()
            {
                Id = (Serial)0x702u,
                Name = "Agility Potion",
                ScriptId = "items.agility_potion",
                ItemId = 0x0F08,
                Amount = 1,
                MapId = 0,
                Location = Point3D.Zero
            }
        );

        var dispatched = await environment.Dispatcher.DispatchAsync(
            new ItemScriptContext(environment.Session, environment.ItemService.Item!, "double_click")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.Mobile.RuntimeModifiers, Is.Not.Null);
                Assert.That(environment.Mobile.RuntimeModifiers!.DexterityBonus, Is.EqualTo(10));
                Assert.That(environment.Mobile.EffectiveDexterity, Is.EqualTo(35));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_WhenStrengthPotionIsUsed_ShouldApplyTemporaryStrengthBonus()
    {
        using var environment = await CreateEnvironmentAsync(
            "strength_potion",
            mobile: new()
            {
                Id = (Serial)0x504u,
                Name = "Potion Tester",
                IsAlive = true,
                Strength = 30,
                Hits = 30,
                MaxHits = 30
            },
            item: new()
            {
                Id = (Serial)0x703u,
                Name = "Strength Potion",
                ScriptId = "items.strength_potion",
                ItemId = 0x0F09,
                Amount = 1,
                MapId = 0,
                Location = Point3D.Zero
            }
        );

        var dispatched = await environment.Dispatcher.DispatchAsync(
            new ItemScriptContext(environment.Session, environment.ItemService.Item!, "double_click")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.Mobile.RuntimeModifiers, Is.Not.Null);
                Assert.That(environment.Mobile.RuntimeModifiers!.StrengthBonus, Is.EqualTo(10));
                Assert.That(environment.Mobile.EffectiveStrength, Is.EqualTo(40));
            }
        );
    }

    [TestCase("lesser_cure_potion", 0x0F07)]
    [TestCase("lesser_poison_potion", 0x0F0A)]
    [TestCase("lesser_explosion_potion", 0x0F0D)]
    public async Task DispatchAsync_WhenNoOpPotionIsUsed_ShouldConsumeWithoutThrowing(string scriptName, int itemId)
    {
        using var environment = await CreateEnvironmentAsync(
            scriptName,
            mobile: new()
            {
                Id = (Serial)0x505u,
                Name = "Potion Tester",
                IsAlive = true,
                Hits = 30,
                MaxHits = 30
            },
            item: new()
            {
                Id = (Serial)0x704u,
                Name = scriptName,
                ScriptId = $"items.{scriptName}",
                ItemId = itemId,
                Amount = 1,
                MapId = 0,
                Location = Point3D.Zero
            }
        );

        var dispatched = await environment.Dispatcher.DispatchAsync(
            new ItemScriptContext(environment.Session, environment.ItemService.Item!, "double_click")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.ItemService.Item, Is.Null);
                Assert.That(environment.Mobile.Hits, Is.EqualTo(30));
            }
        );
    }

    private static async Task<PotionScriptEnvironment> CreateEnvironmentAsync(
        string primaryScriptName,
        UOMobileEntity mobile,
        UOItemEntity item
    )
    {
        var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        Directory.CreateDirectory(Path.Combine(scriptsDirectory, "items"));

        var initContents = string.Join(
            Environment.NewLine,
            [
                "require(\"items.agility_potion\")",
                "require(\"items.strength_potion\")",
                "require(\"items.refresh_potion\")",
                "require(\"items.lesser_cure_potion\")",
                "require(\"items.lesser_heal_potion\")",
                "require(\"items.lesser_poison_potion\")",
                "require(\"items.lesser_explosion_potion\")"
            ]
        );
        await File.WriteAllTextAsync(Path.Combine(scriptsDirectory, "init.lua"), initContents);

        await CopyPotionScriptAsync("potion_common.lua", scriptsDirectory);
        await CopyPotionScriptAsync($"{primaryScriptName}.lua", scriptsDirectory);

        if (primaryScriptName is not ("agility_potion" or "strength_potion" or "refresh_potion" or "lesser_cure_potion" or "lesser_heal_potion" or "lesser_poison_potion" or "lesser_explosion_potion"))
        {
            throw new InvalidOperationException($"Unexpected potion script '{primaryScriptName}'.");
        }

        foreach (var required in new[]
                 {
                     "agility_potion.lua",
                     "strength_potion.lua",
                     "refresh_potion.lua",
                     "lesser_cure_potion.lua",
                     "lesser_heal_potion.lua",
                     "lesser_poison_potion.lua",
                     "lesser_explosion_potion.lua"
                 })
        {
            if (!File.Exists(Path.Combine(scriptsDirectory, "items", required)))
            {
                await CopyPotionScriptAsync(required, scriptsDirectory);
            }
        }

        var itemService = new PotionItemScriptsTestItemService { Item = item };
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var backgroundJobs = new PotionItemScriptsTestBackgroundJobService();
        var container = new Container();
        container.RegisterInstance<IItemService>(itemService);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IOutgoingPacketQueue>(outgoingQueue);
        container.RegisterInstance<IBackgroundJobService>(backgroundJobs);

        var scriptEngine = new LuaScriptEngineService(
            directories,
            [new(typeof(ConvertModule)), new(typeof(ItemModule)), new(typeof(PotionEffectsModule))],
            container,
            new LuaEngineConfig(temp.Path, scriptsDirectory, "0.1.0"),
            []
        );
        await scriptEngine.StartAsync();

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };
        sessionService.Add(session);

        var dispatcher = new ItemScriptDispatcher(scriptEngine, itemService, sessionService);

        return new(temp, scriptEngine, dispatcher, session, mobile, itemService);
    }

    private static Task CopyPotionScriptAsync(string fileName, string scriptsDirectory)
    {
        File.Copy(
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
                    fileName
                )
            ),
            Path.Combine(scriptsDirectory, "items", fileName),
            true
        );

        return Task.CompletedTask;
    }

    private sealed class PotionScriptEnvironment : IDisposable
    {
        public PotionScriptEnvironment(
            TempDirectory tempDirectory,
            LuaScriptEngineService scriptEngine,
            ItemScriptDispatcher dispatcher,
            GameSession session,
            UOMobileEntity mobile,
            PotionItemScriptsTestItemService itemService
        )
        {
            TempDirectory = tempDirectory;
            ScriptEngine = scriptEngine;
            Dispatcher = dispatcher;
            Session = session;
            Mobile = mobile;
            ItemService = itemService;
        }

        public TempDirectory TempDirectory { get; }

        public LuaScriptEngineService ScriptEngine { get; }

        public ItemScriptDispatcher Dispatcher { get; }

        public GameSession Session { get; }

        public UOMobileEntity Mobile { get; }

        public PotionItemScriptsTestItemService ItemService { get; }

        public void Dispose()
        {
            ScriptEngine.Dispose();
            TempDirectory.Dispose();
        }
    }
}
