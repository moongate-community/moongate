using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class StartingItemsServiceTests : IAsyncLifetime
{
    private HostPersistenceFixture _host = null!;
    private IDataAccess<ItemEntity> _items = null!;
    private MobileEntity _mobile = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _items = _host.Container.Resolve<IDataAccess<ItemEntity>>();
        _mobile = new MobileEntity { Id = new(0x00000100 + (uint)Random.Shared.Next(1, 100000)), Name = "Aria" };
        await _host.Container.Resolve<IDataAccess<MobileEntity>>().UpsertAsync(_mobile);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task GiveAsync_FullFlow()
    {
        var service = CreateService(
            Set(skill: SkillType.Alchemy, entries: [Entry("pearl", "3"), Entry("shirt", equip: true)]),
            Set(common: true, entries: [Entry("bottle", "2"), Entry("fancy_shirt", equip: true), Entry("gold", "1000")]),
            Set(race: RaceType.Human, gender: GenderType.Male, entries: [Entry("pants", equip: true)]),
            Set(race: RaceType.Elf, entries: [Entry("elven_boots", equip: true)]),
            Set(skill: SkillType.Magery, entries: [Entry("spellbook")])
        );

        var given = await service.GiveAsync(Request(new() { [SkillType.Alchemy] = 50, [SkillType.Magery] = 0 }, 0x20, 0));

        var backpack = given[0];
        Assert.Equal((_mobile.Id, LayerType.Backpack), (backpack.MobileId!.Value, backpack.Layer!.Value));
        var stored = await _items.QueryAsync(item => item.MobileId == _mobile.Id || item.ContainerId == backpack.Id);
        Assert.Equal(given.Count, stored.Count);

        var shirt = stored.Single(item => item.TemplateId == "shirt");
        Assert.Equal((LayerType.Shirt, (ushort)0x20), (shirt.Layer!.Value, shirt.Hue.Value));

        // A second item on the Shirt layer goes to the backpack.
        Assert.Equal(backpack.Id, stored.Single(item => item.TemplateId == "fancy_shirt").ContainerId);

        // PantsHue 0 keeps the template hue.
        var pants = stored.Single(item => item.TemplateId == "pants");
        Assert.Equal((LayerType.Pants, (ushort)0x100), (pants.Layer!.Value, pants.Hue.Value));

        Assert.Equal(3, stored.Single(item => item.TemplateId == "pearl").Amount);
        Assert.Equal(2, stored.Count(item => item.TemplateId == "bottle"));
        Assert.Equal(1000, stored.Single(item => item.TemplateId == "gold").Amount);
        Assert.DoesNotContain(stored, item => item.TemplateId is "elven_boots" or "spellbook");
        Assert.All(
            stored.Where(item => item.ContainerId == backpack.Id),
            item => Assert.True(item.GridLocation!.Value is { X: >= 44 and < 186, Y: >= 65 and < 159 })
        );
        Assert.Equal(LootType.Newbied, stored.Single(item => item.TemplateId == "pearl").GetProp<LootType>(ItemPropKeys.LootType));
    }

    [Fact]
    public async Task GiveAsync_NoSetWithGold_GivesNoGold()
    {
        var service = CreateService(Set(common: true, entries: [Entry("bottle")]));

        var given = await service.GiveAsync(Request(new()));

        Assert.DoesNotContain(given, item => item.TemplateId == "gold");
    }

    [Fact]
    public async Task GiveAsync_AllSkillsZero_GetsCommonAndBodySetsOnly()
    {
        var service = CreateService(
            Set(skill: SkillType.Alchemy, entries: [Entry("pearl")]),
            Set(common: true, entries: [Entry("bottle")])
        );

        var given = await service.GiveAsync(Request(new() { [SkillType.Alchemy] = 0 }));

        Assert.Equal(["backpack", "bottle"], given.Select(item => item.TemplateId));
    }

    [Fact]
    public async Task GiveAsync_NewbieFalse_StoresNothingWhenTheTemplateIsRegular()
    {
        var service = CreateService(Set(common: true, entries: [Entry("bottle", newbie: false)]));

        var given = await service.GiveAsync(Request(new()));

        Assert.Null(given.Single(item => item.TemplateId == "bottle").Props);
    }

    [Fact]
    public async Task GiveAsync_AFailure_RollsEverythingBack()
    {
        // An unknown template makes Create throw after the other items were saved.
        var service = CreateService(Set(common: true, entries: [Entry("bottle"), Entry("missing")]));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GiveAsync(Request(new())));

        Assert.Empty(await _items.QueryAsync(item => item.MobileId == _mobile.Id));
    }

    [Fact]
    public async Task GiveAsync_InACallersTransaction_IsRolledBackWithIt()
    {
        var service = CreateService(Set(common: true, entries: [Entry("bottle")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _host.Owner.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Realm,
                async transaction =>
                {
                    Assert.NotEmpty(await service.GiveAsync(transaction, Request(new())));

                    throw new InvalidOperationException("caller failed");
                },
                CancellationToken.None
            )
        );

        Assert.Empty(await _items.QueryAsync(item => item.MobileId == _mobile.Id));
    }

    [Fact]
    public async Task StartAsync_AMissingConfiguredTemplate_Throws()
    {
        var service = CreateService(new StartingItemsConfig(), new ItemsConfig { BackpackTemplate = "chest", GoldTemplate = "gold" });

        await Assert.ThrowsAsync<InvalidDataException>(service.StartAsync);
    }

    [Fact]
    public async Task StartAsync_AGoldTemplateThatDoesNotStack_Throws()
    {
        var service = CreateService(new StartingItemsConfig(), new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = "shirt" });

        await Assert.ThrowsAsync<InvalidDataException>(service.StartAsync);
    }

    private StartingItemsService CreateService(params StartingItemSet[] sets)
    {
        return CreateService(new StartingItemsConfig(), new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = "gold" }, sets);
    }

    private StartingItemsService CreateService(StartingItemsConfig config, ItemsConfig items, params StartingItemSet[] sets)
    {
        var loaders = new StubDataLoaderService()
                      .With(
                          Template("backpack", 0x0E75),
                          Template("gold", 0x0EED),
                          Template("pearl", 0x0F7A),
                          Template("bottle", 0x0F0E),
                          Template("shirt", 0x1517),
                          Template("fancy_shirt", 0x1EFD),
                          Template("pants", 0x152E, 0x100),
                          Template("elven_boots", 0x2FC4),
                          Template("spellbook", 0x0EFA)
                      )
                      .With(new ContainerContent { Name = "default", Bounds = new(new Point2D(44, 65), new Point2D(186, 159)), Default = true })
                      .With<StartingItemSet>(sets);
        var tiles = new FakeTileDataService()
                    .Item(0x0E75, TileFlagType.Container, 0, layer: (byte)LayerType.Backpack)
                    .Item(0x0EED, TileFlagType.Generic, 0)
                    .Item(0x0F7A, TileFlagType.Generic, 0)
                    .Item(0x0F0E, TileFlagType.None, 0)
                    .Item(0x1517, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shirt)
                    .Item(0x1EFD, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shirt)
                    .Item(0x152E, TileFlagType.Wearable, 0, layer: (byte)LayerType.Pants)
                    .Item(0x2FC4, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shoes)
                    .Item(0x0EFA, TileFlagType.None, 0);
        var templates = new ItemTemplateService(loaders);

        return new StartingItemsService(
            loaders,
            new ItemFactoryService(templates, tiles, _host.Owner),
            templates,
            new ContainerLayoutService(loaders),
            tiles,
            _host.Owner,
            config,
            items
        );
    }

    private StartingItemsRequest Request(Dictionary<SkillType, int> skills, ushort shirtHue = 0, ushort pantsHue = 0)
    {
        return new(_mobile.Id, RaceType.Human, GenderType.Male, skills, new Hue(shirtHue), new Hue(pantsHue));
    }

    private static ItemTemplate Template(string id, int itemId, int hue = 0)
    {
        return new() { Id = id, ItemId = new Serial((uint)itemId), Hue = HueSpec.FromValue(hue) };
    }

    private static StartingItemSet Set(
        bool common = false,
        SkillType? skill = null,
        RaceType? race = null,
        GenderType? gender = null,
        List<StartingItemEntry>? entries = null
    )
    {
        return new() { Common = common, Skill = skill, Race = race, Gender = gender, Items = entries ?? [] };
    }

    private static StartingItemEntry Entry(string item, string? amount = null, bool equip = false, bool? newbie = null)
    {
        return new() { Items = [item], Amount = amount is null ? null : DiceSpec.Parse(amount), Equip = equip, Newbie = newbie };
    }
}
