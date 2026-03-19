using System.Globalization;
using Moongate.Core.Extensions.Strings;
using Moongate.Server.Commands.WorldGen;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.WorldGen;

public sealed class SpawnDecorationsCommandTests
{
    private sealed class SpawnDecorationsTestSeedDataService : ISeedDataService
    {
        private readonly IReadOnlyList<DecorationEntry> _decorations;

        public SpawnDecorationsTestSeedDataService(IReadOnlyList<DecorationEntry> decorations)
        {
            _decorations = decorations;
        }

        public IReadOnlyList<DecorationEntry> GetDecorationsByMap(int mapId)
            => mapId == 0 ? _decorations : [];

        public IReadOnlyList<DoorComponentEntry> GetDoors()
            => [];

        public IReadOnlyList<WorldLocationEntry> GetLocations()
            => [];

        public IReadOnlyList<SignEntry> GetSignsByMap(int mapId)
            => [];

        public IReadOnlyList<SpawnDefinitionEntry> GetSpawnsByMap(int mapId)
            => [];

        public IReadOnlyList<TeleporterEntry> GetTeleportersBySourceMap(int mapId)
            => [];
    }

    private sealed class ImmediateBackgroundJobService : IBackgroundJobService
    {
        public void EnqueueBackground(Action job)
            => job();

        public void EnqueueBackground(Func<Task> job)
            => job().GetAwaiter().GetResult();

        public int ExecutePendingOnGameLoop(int maxActions = 100)
            => 0;

        public void PostToGameLoop(Action action)
            => action();

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            try
            {
                var result = backgroundJob();
                onGameLoopResult(result);
            }
            catch (Exception ex) when (onGameLoopError is not null)
            {
                onGameLoopError(ex);
            }
        }

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            try
            {
                var result = backgroundJob().GetAwaiter().GetResult();
                onGameLoopResult(result);
            }
            catch (Exception ex) when (onGameLoopError is not null)
            {
                onGameLoopError(ex);
            }
        }

        public void Start(int? workerCount = null) { }

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class SpawnDecorationsTestItemFactoryService : IItemFactoryService
    {
        public Dictionary<string, ItemTemplateDefinition> ExistingTemplates { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            var itemId = 0x1BC3;
            var scriptId = "none";

            if (ExistingTemplates.TryGetValue(itemTemplateId, out var definition))
            {
                itemId = ParseItemId(definition.ItemId);
                scriptId = definition.ScriptId;
            }

            return new()
            {
                Id = (Serial)0x40000001,
                Name = itemTemplateId,
                ItemId = itemId,
                MapId = 0,
                Location = Point3D.Zero,
                ScriptId = scriptId,
                Direction = DirectionType.North
            };
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
        {
            if (ExistingTemplates.TryGetValue(itemTemplateId, out definition))
            {
                return true;
            }

            var snakeCase = itemTemplateId.ToSnakeCase();

            if (ExistingTemplates.TryGetValue(snakeCase, out definition))
            {
                return true;
            }

            definition = null;

            return false;
        }

        private static int ParseItemId(string value)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return int.Parse(value, CultureInfo.InvariantCulture);
        }
    }

    private sealed class SpawnDecorationsTestItemService : IItemService
    {
        public List<UOItemEntity> CreatedItems { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            CreatedItems.Add(item);

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(true);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldApplyDoorFacingMetadata_WhenFacingParameterIsPresent()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Facing"] = DoorGenerationFacing.EastCCW.ToString()
        };
        var decoration = new DecorationEntry(
            0,
            "Britannia",
            "sample.cfg",
            "MetalDoor",
            (Serial)0x0675,
            parameters,
            new(150, 250, 10),
            string.Empty
        );
        var seedData = new SpawnDecorationsTestSeedDataService([decoration]);
        var background = new ImmediateBackgroundJobService();
        var itemFactory = new SpawnDecorationsTestItemFactoryService();
        var itemService = new SpawnDecorationsTestItemService();
        var command = new SpawnDecorationsCommand(seedData, background, itemFactory, itemService);

        var context = new CommandSystemContext(
            "spawn_decorations 0",
            ["0"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(itemService.CreatedItems, Has.Count.EqualTo(1));
        var created = itemService.CreatedItems[0];

        Assert.Multiple(
            () =>
            {
                Assert.That(created.ItemId, Is.EqualTo(DoorGenerationFacing.EastCCW.ToItemId(0x0675)));
                Assert.That(created.Direction, Is.EqualTo(DoorGenerationFacing.EastCCW.ToDirectionType()));
                Assert.That(created.TryGetCustomString("door_facing", out var facing), Is.True);
                Assert.That(facing, Is.EqualTo(DoorGenerationFacing.EastCCW.ToString()));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldMapDecorationParametersToCustomTypedProperties()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Substring"] = "om om om",
            ["Range"] = "0",
            ["PointDest"] = "(1595, 2490, 20)",
            ["SourceEffect"] = "true",
            ["DestEffect"] = "true",
            ["SoundID"] = "0x1FE",
            ["Delay"] = "0:0:1"
        };
        var decoration = new DecorationEntry(
            0,
            "Felucca",
            "test.cfg",
            "KeywordTeleporter",
            (Serial)0x1BC3,
            parameters,
            new(100, 200, 5),
            string.Empty
        );
        var seedData = new SpawnDecorationsTestSeedDataService([decoration]);
        var background = new ImmediateBackgroundJobService();
        var itemFactory = new SpawnDecorationsTestItemFactoryService();
        var itemService = new SpawnDecorationsTestItemService();
        var command = new SpawnDecorationsCommand(seedData, background, itemFactory, itemService);

        var context = new CommandSystemContext(
            "spawn_decorations 0",
            ["0"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(itemService.CreatedItems, Has.Count.EqualTo(1));
        var created = itemService.CreatedItems[0];

        Assert.Multiple(
            () =>
            {
                Assert.That(created.Location, Is.EqualTo(new Point3D(100, 200, 5)));
                Assert.That(created.MapId, Is.EqualTo(0));
                Assert.That(created.TryGetCustomString("substring", out var substring), Is.True);
                Assert.That(substring, Is.EqualTo("om om om"));
                Assert.That(created.TryGetCustomInteger("range", out var range), Is.True);
                Assert.That(range, Is.EqualTo(0));
                Assert.That(created.TryGetCustomLocation("point_dest", out var pointDest), Is.True);
                Assert.That(pointDest, Is.EqualTo(new Point3D(1595, 2490, 20)));
                Assert.That(created.TryGetCustomBoolean("source_effect", out var sourceEffect), Is.True);
                Assert.That(sourceEffect, Is.True);
                Assert.That(created.TryGetCustomBoolean("dest_effect", out var destEffect), Is.True);
                Assert.That(destEffect, Is.True);
                Assert.That(created.TryGetCustomInteger("sound_id", out var soundId), Is.True);
                Assert.That(soundId, Is.EqualTo(0x1FE));
                Assert.That(created.TryGetCustomInteger("delay_ms", out var delayMs), Is.True);
                Assert.That(delayMs, Is.EqualTo(1000));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldPreserveDecorationItemId_WhenTypedTemplateExists()
    {
        var decoration = new DecorationEntry(
            0,
            "Britannia",
            "sample.cfg",
            "RuinedBookcase",
            (Serial)0x0A99,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new(150, 250, 10),
            string.Empty
        );
        var seedData = new SpawnDecorationsTestSeedDataService([decoration]);
        var background = new ImmediateBackgroundJobService();
        var itemFactory = new SpawnDecorationsTestItemFactoryService
        {
            ExistingTemplates =
            {
                ["RuinedBookcase"] = new()
                {
                    Id = "ruined_bookcase",
                    Name = "Ruined Bookcase",
                    ItemId = "0x0A97",
                    Hue = HueSpec.FromValue(0),
                    GoldValue = GoldValueSpec.FromValue(0),
                    ScriptId = "items.ruined_bookcase",
                    IsMovable = true
                }
            }
        };
        var itemService = new SpawnDecorationsTestItemService();
        var command = new SpawnDecorationsCommand(seedData, background, itemFactory, itemService);

        var context = new CommandSystemContext(
            "spawn_decorations 0",
            ["0"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(itemService.CreatedItems, Has.Count.EqualTo(1));
        Assert.That(itemService.CreatedItems[0].ItemId, Is.EqualTo(0x0A99));
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldPreserveDecorationItemId_WhenUsingFallbackStaticTemplate()
    {
        var decoration = new DecorationEntry(
            0,
            "Britannia",
            "britain.cfg",
            "LibraryBookcase",
            (Serial)0x0A98,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new(100, 200, 5),
            string.Empty
        );
        var seedData = new SpawnDecorationsTestSeedDataService([decoration]);
        var background = new ImmediateBackgroundJobService();
        var itemFactory = new SpawnDecorationsTestItemFactoryService();
        var itemService = new SpawnDecorationsTestItemService();
        var command = new SpawnDecorationsCommand(seedData, background, itemFactory, itemService);

        var context = new CommandSystemContext(
            "spawn_decorations 0",
            ["0"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(itemService.CreatedItems, Has.Count.EqualTo(1));
        Assert.That(itemService.CreatedItems[0].ItemId, Is.EqualTo(0x0A98));
    }
}
