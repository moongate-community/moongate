using Moongate.Server.Ultima.Services.Internal.Books;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Ultima.Books;
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
    private BookTemplate _source = null!;
    private CountingBookAttachmentPreparationService _preparation = null!;

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

        // Each item in the backpack has its own grid slot, or the Enhanced Client shows one item of them all.
        var slots = stored.Where(item => item.ContainerId == backpack.Id).Select(item => item.GridIndex).ToList();
        Assert.True(slots.Count > 1);
        Assert.Equal(Enumerable.Range(0, slots.Count).Select(slot => (short?)slot), slots.Order());

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
        Assert.Equal(
            LootType.Newbied,
            stored.Single(item => item.TemplateId == "pearl").GetProp<LootType>(ItemPropKeys.LootType)
        );
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
        var service = CreateService(
            new StartingItemsConfig(),
            new ItemsConfig { BackpackTemplate = "chest", GoldTemplate = "gold" }
        );

        await Assert.ThrowsAsync<InvalidDataException>(service.StartAsync);
    }

    [Fact]
    public async Task StartAsync_AGoldTemplateThatDoesNotStack_Throws()
    {
        var service = CreateService(
            new StartingItemsConfig(),
            new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = "shirt" }
        );

        await Assert.ThrowsAsync<InvalidDataException>(service.StartAsync);
    }

    // The blank book of a new character: its own name as the author, and pages it writes in.
    [Fact]
    public async Task GiveAsync_ABlankBook_IsWritable_WithTheCharactersNameAsAuthor()
    {
        var service = CreateService(
            Set(common: true, entries: [new StartingItemEntry { Items = ["readable_book"], BookTemplate = "blank_book" }])
        );

        var given = await service.GiveAsync(Request(new()));

        var book = Assert.Single(await _items.QueryAsync(item => item.ContainerId == given[0].Id));
        Assert.Equal(
            ("readable_book", "a book", "Aria", ""),
            (book.TemplateId, book.Name, book.GetProp<string>("book.author"), book.GetProp<string>("book.content"))
        );
        Assert.True(book.GetProp<bool>("book.writable"));
        Assert.Equal(20, book.GetProp<long>("book.pages"));
    }

    [Fact]
    public async Task GiveAsync_MultipleLetters_SaveIndependentSnapshotsInTheBackpack()
    {
        var service = CreateService(
            Set(
                common: true,
                entries:
                [
                    new StartingItemEntry
                    {
                        Items = ["readable_scroll"], Amount = DiceSpec.Parse("2"), BookTemplate = "welcome_letter",
                        BookValues = new() { ["contact_name"] = 42 }
                    }
                ]
            )
        );
        var given = await service.GiveAsync(Request(new()));
        var backpack = given[0];
        var letters = await _items.QueryAsync(item => item.ContainerId == backpack.Id);
        Assert.Equal(2, letters.Count);
        Assert.Equal(2, letters.Select(item => item.Id).Distinct().Count());
        Assert.All(letters, item => Assert.Equal("Meet 42", item.GetProp<string>("book.content")));
        Assert.All(letters, item => Assert.Equal("Welcome Aria", item.Name));
        letters[0].SetProp("book.content", "Edited");
        Assert.Equal("Meet 42", letters[1].GetProp<string>("book.content"));
    }

    [Fact]
    public async Task StartingItems_AttachmentPayloadSavedInCharacterTransaction()
    {
        var service = CreateService(
            Set(
                common: true,
                entries:
                [
                    new StartingItemEntry
                    {
                        Items = ["readable_scroll"], Amount = DiceSpec.Parse("2"), BookTemplate = "welcome_letter",
                        BookValues = new() { ["contact_name"] = "Vega" }
                    }
                ]
            )
        );
        _source.Attachments.Add(
            new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100"), Hue = HueSpec.FromValue(42) }
        );
        var given = await service.GiveAsync(Request(new()));
        var stored = await _items.QueryAsync(item => item.ContainerId == given[0].Id);
        Assert.Equal(2, _preparation.Calls);
        Assert.Equal(2, stored.Count);
        Assert.All(
            stored,
            letter =>
            {
                Assert.True(
                    BookAttachmentCodec.TryDecode(letter.GetProp<string>(BookAttachmentCodec.PropKey), out var batch)
                );
                Assert.Equal((100, (ushort)42), (Assert.Single(batch!.Items).Amount, batch.Items[0].Hue));
            }
        );
        given[1].SetProp(BookAttachmentCodec.PropKey, "changed");
        Assert.NotEqual("changed", given[2].GetProp<string>(BookAttachmentCodec.PropKey));
        Assert.DoesNotContain(await _items.GetAllAsync(), item => item.TemplateId == "gold");
    }

    [Fact]
    public async Task GiveAsync_LateAttachmentPreparationFailure_RollsBackEarlierItemsAndLetters()
    {
        var service = CreateService(
            Set(
                common: true,
                entries:
                [
                    Entry("bottle"), new StartingItemEntry
                    {
                        Items = ["readable_scroll"], Amount = DiceSpec.Parse("2"), BookTemplate = "welcome_letter",
                        BookValues = new() { ["contact_name"] = "Vega" }
                    }
                ]
            )
        );
        _source.Attachments.Add(new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100") });
        _preparation.FailOnCall = 2;
        await Assert.ThrowsAsync<InvalidDataException>(() => service.GiveAsync(Request(new())));
        Assert.Equal(2, _preparation.Calls);
        Assert.Empty(await _items.GetAllAsync());
    }

    private StartingItemsService CreateService(params StartingItemSet[] sets)
    {
        return CreateService(
            new StartingItemsConfig(),
            new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = "gold" },
            sets
        );
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
                Template("spellbook", 0x0EFA),
                new ItemTemplate
                    { Id = "readable_scroll", ItemId = new(0x14ED), Stackable = false, ScriptId = "readable_scroll" },
                new ItemTemplate
                    { Id = "readable_book", ItemId = new(0x0FF1), Stackable = false, ScriptId = "readable_book" }
            )
            .With(
                new ContainerContent
                    { Name = "default", Bounds = new(new Point2D(44, 65), new Point2D(186, 159)), Default = true }
            )
            .With<StartingItemSet>(sets);
        _source = new BookTemplate
        {
            Id = "welcome_letter", Title = "Welcome $player_name", Content = "Meet $contact_name",
            Variables = ["contact_name"]
        };
        loaders.With(
            _source,
            new BookTemplate
            {
                Id = "blank_book", Title = "a book", Author = "$player_name", ItemTemplate = "readable_book",
                Writable = true, Pages = 20
            }
        );
        var tiles = new FakeTileDataService()
            .Item(0x0E75, TileFlagType.Container, 0, layer: (byte)LayerType.Backpack)
            .Item(0x0EED, TileFlagType.Generic, 0)
            .Item(0x0F7A, TileFlagType.Generic, 0)
            .Item(0x0F0E, TileFlagType.None, 0)
            .Item(0x1517, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shirt)
            .Item(0x1EFD, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shirt)
            .Item(0x152E, TileFlagType.Wearable, 0, layer: (byte)LayerType.Pants)
            .Item(0x2FC4, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shoes)
            .Item(0x0EFA, TileFlagType.None, 0)
            .Item(0x14ED, TileFlagType.None, 1)
            .Item(0x0FF1, TileFlagType.None, 1);
        var templates = new ItemTemplateService(loaders);

        var factory = new ItemFactoryService(templates, tiles, _host.Owner);
        _preparation = new(new BookAttachmentPreparationService(factory, templates, tiles));
        return new StartingItemsService(
            loaders,
            factory,
            templates,
            new ContainerLayoutService(loaders),
            tiles,
            _host.Owner,
            config,
            items,
            new BookTemplateService(loaders),
            TestBookContexts.Create(),
            new LocalizationConfig(),
            _preparation
        );
    }

    private StartingItemsRequest Request(Dictionary<SkillType, int> skills, ushort shirtHue = 0, ushort pantsHue = 0)
    {
        return new(_mobile.Id, RaceType.Human, GenderType.Male, skills, new Hue(shirtHue), new Hue(pantsHue))
            { PlayerName = _mobile.Name };
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
        return new()
        {
            Items = [item], Amount = amount is null ? null : DiceSpec.Parse(amount), Equip = equip, Newbie = newbie
        };
    }
}
