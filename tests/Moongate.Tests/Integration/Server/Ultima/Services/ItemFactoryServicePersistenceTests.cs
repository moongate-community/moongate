using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class ItemFactoryServicePersistenceTests : IAsyncLifetime
{
    private HostPersistenceFixture _host = null!;
    private IDataAccess<ItemEntity> _items = null!;
    private ItemFactoryService _factory = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _items = _host.Container.Resolve<IDataAccess<ItemEntity>>();
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate
                {
                    Id = "gem",
                    ItemId = new Serial(0x0F10),
                    Rarity = EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Legendary)
                }
            )
        );
        _factory = new ItemFactoryService(
            templates,
            new FakeTileDataService().Item(0x0F10, TileFlagType.None, 0),
            _host.Owner
        );
    }

    [Fact]
    public async Task SaveAsync_GivesAnItemSerial_AndKeepsTheRarity()
    {
        var gem = _factory.Create("gem");
        gem.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0));

        await _factory.SaveAsync(gem);

        Assert.InRange(gem.Id.Value, Serial.MinItem, Serial.MaxItem);
        Assert.Equal(ItemRarityType.Legendary, (await _items.GetByIdAsync(gem.Id))!.Rarity);
    }

    [Fact]
    public async Task SaveAsync_ItemsInOrder_ContainerBeforeContents()
    {
        var chest = _factory.Create("gem");
        chest.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0));
        await _factory.SaveAsync(chest);
        var a = _factory.Create("gem");
        var b = _factory.Create("gem");
        a.PutInContainer(chest.Id, new Point2D(1, 1));
        b.PutInContainer(chest.Id, new Point2D(2, 2));

        await _factory.SaveAsync([a, b]);

        Assert.Equal(2, (await _items.QueryAsync(item => item.ContainerId == chest.Id)).Count);
    }

    [Fact]
    public async Task SaveAsync_AnItemWithNoLocation_IsRejectedBeforeTheDatabase()
    {
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.SaveAsync(_factory.Create("gem")));

        Assert.Contains("no location", exception.Message);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }
}
