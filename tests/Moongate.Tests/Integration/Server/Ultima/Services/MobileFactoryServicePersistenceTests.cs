using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Events;
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
    private InProcessEventBus _bus = null!;
    private MobileFactoryService _factory = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _mobiles = _host.Container.Resolve<IDataAccess<MobileEntity>>();
        _items = _host.Container.Resolve<IDataAccess<ItemEntity>>();
        _bus = new InProcessEventBus();
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
                          new ItemTemplate { Id = "torch_on_wall", ItemId = new Serial(0x0A12) }
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
                    .Item(0x0A12, TileFlagType.None, 0, layer: 29);
        var itemTemplates = new ItemTemplateService(loaders);
        _factory = new MobileFactoryService(
            new MobileTemplateService(loaders),
            new NameService(loaders),
            loaders,
            new ItemFactoryService(itemTemplates, tiles, _host.Owner),
            itemTemplates,
            tiles,
            new FakeMapService(200, 200),
            _bus,
            _host.Owner
        );
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task SpawnAsync_SavesTheMobileInTheMobileRange_Dressed_ByGender_DroppingConflicts()
    {
        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));

        var mobile = spawned.Mobile;
        Assert.InRange(mobile.Id.Value, Serial.MinMobile, Serial.MaxMobile);
        var loaded = (await _mobiles.GetByIdAsync(mobile.Id))!;
        Assert.Equal(("guard", MapType.Felucca, new Point3D(10, 20, 0)), (loaded.TemplateId, loaded.Map, loaded.Location));
        var worn = (await _items.QueryAsync(item => item.MobileId == mobile.Id)).Select(item => item.TemplateId).Order().ToList();
        Assert.Equal(mobile.Gender == GenderType.Male ? ["helm", "pants"] : ["helm", "skirt"], worn);
        Assert.Equal(worn.Count, spawned.Equipment.Count);
    }

    [Fact]
    public async Task SpawnAsync_OutsideTheMap_WritesNothingAndPublishesNothing()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(500, 20, 0))
        );

        Assert.Empty(_bus.Published);
        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "guard"));
    }

    [Fact]
    public async Task SpawnAsync_PublishesTheEventsInOrder_AndBeforeSpawnChangesAreSaved()
    {
        _bus.Subscribe<MobileBeforeSpawnEvent>(
            (e, _) =>
            {
                e.Mobile.Name = "Captain Rook";

                return Task.CompletedTask;
            }
        );

        var spawned = await _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0));

        Assert.Equal(
            [typeof(MobileBeforeSpawnEvent), typeof(MobileMovedToWorldEvent), typeof(MobileAfterSpawnEvent)],
            _bus.Published.Select(e => e.GetType())
        );
        Assert.Equal("Captain Rook", (await _mobiles.GetByIdAsync(spawned.Mobile.Id))!.Name);
        Assert.Same(spawned, ((MobileAfterSpawnEvent)_bus.Published[2]).Spawned);
    }

    [Fact]
    public async Task SpawnAsync_ABeforeSpawnHandlerThatThrows_SavesNothing()
    {
        _bus.Subscribe<MobileBeforeSpawnEvent>((_, _) => throw new InvalidOperationException("no guards today"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _factory.SpawnAsync("guard", MapType.Felucca, new Point3D(10, 20, 0))
        );

        Assert.Single(_bus.Published);
        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "guard"));
    }

    [Fact]
    public async Task SpawnAsync_AnEquipmentFailure_RollsTheMobileBack()
    {
        // The loader would have caught this item; here it vanishes after startup, so the item lookup throws.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _factory.SpawnAsync("broken", MapType.Felucca, new Point3D(10, 20, 0))
        );

        Assert.Empty(await _mobiles.QueryAsync(m => m.TemplateId == "broken"));
        Assert.DoesNotContain(_bus.Published, e => e is MobileMovedToWorldEvent or MobileAfterSpawnEvent);
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
