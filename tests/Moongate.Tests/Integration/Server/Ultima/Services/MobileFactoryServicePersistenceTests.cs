using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class MobileFactoryServicePersistenceTests : IAsyncLifetime
{
    private HostPersistenceFixture _host = null!;
    private IDataAccess<MobileEntity> _mobiles = null!;
    private IDataAccess<ItemEntity> _items = null!;
    private readonly List<IMoongateEvent> _published = [];
    private Container _busContainer = null!;
    private IMoongateEventBus _bus = null!;
    private MobileFactoryService _factory = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _mobiles = _host.Container.Resolve<IDataAccess<MobileEntity>>();
        _items = _host.Container.Resolve<IDataAccess<ItemEntity>>();
        // The real bus: it runs handlers in order and logs, rather than rethrows, a handler's exception.
        _busContainer = new Container();
        _busContainer.RegisterMoongateEventBus();
        _bus = _busContainer.Resolve<IMoongateEventBus>();
        _bus.SubscribeAll((e, _) =>
            {
                _published.Add(e);

                return Task.CompletedTask;
            }
        );
        var loaders = new StubDataLoaderService()
            .With(
                new MobileTemplate
                {
                    Id = "guard", Race = RaceType.Human, Gender = MobileGenderType.Random, NameList = "{gender}",
                    Equipment =
                    [
                        new MobileEquipmentEntry { Items = ["helm"] },
                        new MobileEquipmentEntry { Items = ["skirt"], Gender = GenderType.Female },
                        new MobileEquipmentEntry { Items = ["pants"], Gender = GenderType.Male },
                        new MobileEquipmentEntry { Items = ["second_helm"] },
                        new MobileEquipmentEntry { Items = ["torch_on_wall"] }
                    ]
                },
                new MobileTemplate { Id = "shapeless", Name = "a shapeless thing" },
                new MobileTemplate
                {
                    Id = "rich_guard", Body = 400, Gender = MobileGenderType.Male, Gold = DiceSpec.FromValue(70000),
                    Loot = ["gems", "gems"],
                    Equipment =
                    [
                        new MobileEquipmentEntry { Items = ["helm"] }, new MobileEquipmentEntry { Items = ["second_helm"] }
                    ]
                },
                new MobileTemplate { Id = "plain", Body = 17 },
                new MobileTemplate { Id = "lost_loot", Body = 17, Loot = ["vanished"] },
                new MobileTemplate
                {
                    Id = "broken", Body = 17, Equipment = [new MobileEquipmentEntry { Items = ["missing_at_runtime"] }]
                }
            )
            .With(
                new ItemTemplate { Id = "helm", ItemId = new Serial(0x140A) },
                new ItemTemplate { Id = "second_helm", ItemId = new Serial(0x140B) },
                new ItemTemplate { Id = "skirt", ItemId = new Serial(0x1516) },
                new ItemTemplate { Id = "pants", ItemId = new Serial(0x152E) },
                new ItemTemplate { Id = "torch_on_wall", ItemId = new Serial(0x0A12) },
                new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
                new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
                new ItemTemplate { Id = "ruby", ItemId = new Serial(0x0F13) }
            )
            .With(new LootTemplate { Id = "gems", Entries = [new LootEntry { ItemId = "ruby" }] })
            .With(
                new ContainerContent
                    { Name = "default", Bounds = new(new Point2D(44, 65), new Point2D(186, 159)), Default = true }
            )
            .With(
                new RaceContent
                {
                    Race = RaceType.Human, Name = "Human",
                    Male = new RaceGenderContent { Body = 400 }, Female = new RaceGenderContent { Body = 401 }
                }
            )
            .With(new NameList { Id = "male", Names = ["Aaron"] }, new NameList { Id = "female", Names = ["Alice"] });
        var tiles = new FakeTileDataService()
            .Item(0x140A, TileFlagType.Wearable, 0, layer: (byte)LayerType.Helm)
            .Item(0x140B, TileFlagType.Wearable, 0, layer: (byte)LayerType.Helm)
            .Item(0x1516, TileFlagType.Wearable, 0, layer: (byte)LayerType.OuterLegs)
            .Item(0x152E, TileFlagType.Wearable, 0, layer: (byte)LayerType.Pants)
            .Item(0x0A12, TileFlagType.None, 0, layer: 29)
            .Item(0x0E75, TileFlagType.Container, 0, layer: (byte)LayerType.Backpack)
            .Item(0x0EED, TileFlagType.Generic, 0)
            .Item(0x0F13, TileFlagType.None, 0);
        var itemTemplates = new ItemTemplateService(loaders);
        var itemFactory = new ItemFactoryService(itemTemplates, tiles, _host.Owner);
        _factory = new MobileFactoryService(
            new MobileTemplateService(loaders),
            new NameService(loaders),
            loaders,
            itemFactory,
            itemTemplates,
            new LootService(loaders, itemFactory, itemTemplates, tiles),
            new ContainerLayoutService(loaders),
            new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = "gold" },
            tiles,
            new FakeMapService(200, 200),
            _bus,
            _host.Owner
        );
    }

    public async Task DisposeAsync()
    {
        _busContainer.Dispose();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task SpawnAsync_SavesTheMobileInTheMobileRange_Dressed_ByGender_ConflictsGoToTheBackpack()
    {
        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));

        var mobile = spawned.Mobile;
        Assert.InRange(mobile.Id.Value, Serial.MinMobile, Serial.MaxMobile);
        var loaded = (await _mobiles.GetByIdAsync(mobile.Id))!;
        Assert.Equal(("guard", MapType.Felucca, new Point3D(10, 20, 0)), (loaded.TemplateId, loaded.Map, loaded.Location));
        var worn = (await _items.QueryAsync(item => item.MobileId == mobile.Id)).Select(item => item.TemplateId)
            .Order()
            .ToList();
        Assert.Equal(mobile.Gender == GenderType.Male ? ["backpack", "helm", "pants"] : ["backpack", "helm", "skirt"], worn);
        Assert.Equal(worn.Count - 1, spawned.Equipment.Count);
        var packedItems = await _items.QueryAsync(item => item.ContainerId == spawned.Backpack.Id);
        Assert.Equal(["second_helm", "torch_on_wall"], packedItems.Select(item => item.TemplateId).Order());
        Assert.Equal([(short?)0, (short?)1], packedItems.Select(item => item.GridIndex).Order());
    }

    [Fact]
    public async Task SpawnAsync_SavesTheGivenPropsWithTheMobile()
    {
        var props = new Dictionary<string, object?> { ["spawn.region"] = "forest", ["spawn.x1"] = 10L };

        var spawned = await _factory.SpawnAsync("plain", MapType.Felucca, new Point3D(10, 20, 0), props);

        var loaded = (await _mobiles.GetByIdAsync(spawned.Mobile.Id))!;
        Assert.Equal("forest", loaded.GetProp<string>("spawn.region"));
        Assert.Equal(10L, loaded.GetProp<long>("spawn.x1"));
    }

    [Fact]
    public async Task SpawnAsync_APlainNpc_StillWearsABackpack()
    {
        var spawned = await _factory.SpawnAsync("plain", MapType.Felucca, new Point3D(10, 20, 0));

        var backpack = (await _items.GetByIdAsync(spawned.Backpack.Id))!;
        Assert.Equal((spawned.Mobile.Id, (LayerType?)LayerType.Backpack), (backpack.MobileId!.Value, backpack.Layer));
        Assert.Empty(spawned.BackpackItems);
    }

    [Fact]
    public async Task SpawnAsync_GoldAndLoot_AreInTheBackpack_WithGoldSplitInPiles()
    {
        var spawned = await _factory.SpawnAsync("rich_guard", MapType.Felucca, new Point3D(10, 20, 0));

        var inside = await _items.QueryAsync(item => item.ContainerId == spawned.Backpack.Id);
        Assert.Equal([65535, 4465], inside.Where(i => i.TemplateId == "gold").Select(i => i.Amount).OrderDescending());
        Assert.Equal(2, inside.Count(i => i.TemplateId == "ruby"));
        Assert.Contains(inside, i => i.TemplateId == "second_helm");
        Assert.All(inside, i => Assert.True(i.GridLocation!.Value is { X: >= 44 and < 186, Y: >= 65 and < 159 }));
        Assert.Equal(inside.Count, spawned.BackpackItems.Count);
    }

    [Fact]
    public async Task SpawnAsync_ALootTableThatVanished_RollsTheWholeSpawnBack()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _factory.SpawnAsync(
                "lost_loot",
                MapType.Felucca,
                new Point3D(10, 20, 0)
            )
        );

        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "lost_loot"));
        Assert.Empty(await _items.QueryAsync(item => item.TemplateId == "backpack"));
    }

    [Fact]
    public async Task SpawnAsync_OutsideTheMap_WritesNothingAndPublishesNothing()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _factory.SpawnAsync(
                "guard",
                MapType.Felucca,
                new Point3D(500, 20, 0)
            )
        );

        Assert.Empty(_published);
        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "guard"));
    }

    [Fact]
    public async Task SpawnAsync_PublishesTheEventsInOrder_AndBeforeSpawnChangesAreSaved()
    {
        _bus.Subscribe<MobileBeforeSpawnEvent>((e, _) =>
            {
                e.Mobile.Name = "Captain Rook";

                return Task.CompletedTask;
            }
        );

        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));

        Assert.Equal(
            [typeof(MobileBeforeSpawnEvent), typeof(MobileMovedToWorldEvent), typeof(MobileAfterSpawnEvent)],
            _published.Select(e => e.GetType())
        );
        Assert.Equal("Captain Rook", (await _mobiles.GetByIdAsync(spawned.Mobile.Id))!.Name);
        Assert.Same(spawned, ((MobileAfterSpawnEvent)_published[2]).Spawned);
    }

    [Fact]
    public async Task SpawnAsync_AHandlerThatThrows_IsLoggedByTheBus_AndTheSpawnGoesOn()
    {
        _bus.Subscribe<MobileBeforeSpawnEvent>((_, _) => throw new InvalidOperationException("no guards today"));

        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));

        Assert.NotNull(await _mobiles.GetByIdAsync(spawned.Mobile.Id));
        Assert.Equal(3, _published.Count);
    }

    [Fact]
    public async Task SpawnAsync_ATemplateWithNoBodyAndNoRace_IsRejectedBeforeAnything()
    {
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => _factory.SpawnAsync(
                "shapeless",
                MapType.Felucca,
                new Point3D(10, 20, 0)
            )
        );

        Assert.Contains("'shapeless'", exception.Message);
        Assert.Empty(_published);
        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "shapeless"));
    }

    [Fact]
    public async Task SpawnAsync_AnEquipmentFailure_RollsTheMobileBack()
    {
        // The loader would have caught this item; here it vanishes after startup, so the item lookup throws.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _factory.SpawnAsync(
                "broken",
                MapType.Felucca,
                new Point3D(10, 20, 0)
            )
        );

        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "broken"));
        Assert.DoesNotContain(_published, e => e is MobileMovedToWorldEvent or MobileAfterSpawnEvent);
    }

    [Fact]
    public async Task SpawnAsync_ABeforeSpawnMoveOutsideTheMap_IsRejected_AndAMoveInsideIsWhereItIsSaved()
    {
        var move = new Point3D(300, 20, 0);
        _bus.Subscribe<MobileBeforeSpawnEvent>((e, _) =>
            {
                e.Mobile.Location = move;

                return Task.CompletedTask;
            }
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _factory.SpawnAsync(
                "guard",
                MapType.Felucca,
                new Point3D(10, 20, 0)
            )
        );
        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "guard"));

        move = new Point3D(50, 60, 5);
        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));

        Assert.Equal(move, (await _mobiles.GetByIdAsync(spawned.Mobile.Id))!.Location);
        Assert.Equal(move, _published.OfType<MobileMovedToWorldEvent>().Single().Location);
    }

    [Fact]
    public async Task SpawnAsync_AFailureAfterTheMobileUpsert_LeavesItWithoutASerial()
    {
        MobileEntity? seen = null;
        _bus.Subscribe<MobileBeforeSpawnEvent>((e, _) =>
            {
                seen = e.Mobile;

                return Task.CompletedTask;
            }
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _factory.SpawnAsync(
                "broken",
                MapType.Felucca,
                new Point3D(10, 20, 0)
            )
        );

        Assert.Equal(Serial.Zero, seen!.Id);
    }

    [Fact]
    public async Task SpawnAsync_CancelledAfterTheCommit_StillReturnsTheSavedMobile()
    {
        using var cancellation = new CancellationTokenSource();
        _bus.Subscribe<MobileMovedToWorldEvent>((_, _) =>
            {
                cancellation.Cancel();

                return Task.CompletedTask;
            }
        );

        var spawned = await _factory.SpawnAsync(
            "guard",
            MapType.Felucca,
            new Point3D(10, 20, 0),
            cancellationToken: cancellation.Token
        );

        Assert.NotNull(await _mobiles.GetByIdAsync(spawned.Mobile.Id));
        Assert.Contains(_published, e => e is MobileAfterSpawnEvent);
    }

    [Fact]
    public async Task SaveAsync_UpdatesAnExistingMobile_AndRejectsANewOne()
    {
        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));
        spawned.Mobile.Hits = 1;

        await _factory.SaveAsync(spawned.Mobile);

        Assert.Equal(1, (await _mobiles.GetByIdAsync(spawned.Mobile.Id))!.Hits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.SaveAsync(_factory.Create("guard")));
    }
}
