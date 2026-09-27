using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Characters;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class CharacterServiceTests : IAsyncLifetime
{
    private static readonly Serial Account = new(0x4242);

    private readonly Container _eventContainer = new();
    private readonly List<CharacterCreatedEvent> _created = [];
    private HostPersistenceFixture _host = null!;
    private IDataAccess<MobileEntity> _mobiles = null!;
    private IDataAccess<ItemEntity> _items = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _mobiles = _host.Container.Resolve<IDataAccess<MobileEntity>>();
        _items = _host.Container.Resolve<IDataAccess<ItemEntity>>();
        _eventContainer.RegisterMoongateEventBus();
        _eventContainer.Resolve<IMoongateEventBus>()
            .Subscribe<CharacterCreatedEvent>((evt, _) =>
                {
                    _created.Add(evt);

                    return Task.CompletedTask;
                }
            );
    }

    public async Task DisposeAsync()
    {
        _eventContainer.Dispose();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_AdvancedChoice_SavesThePlayerWithItsItems()
    {
        var service = CreateService();

        var result = await service.CreateAsync(Account, Request() with { StartingCity = 1 });

        Assert.True(result.IsCreated);
        var stored = Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Equal(("Aria", (byte?)0, 401, GenderType.Female), (stored.Name, stored.Slot, stored.Body, stored.Gender));
        Assert.Equal((60, 20, 10), (stored.Strength, stored.Dexterity, stored.Intelligence));
        Assert.Equal((60, 60, 20, 20, 10, 10), (stored.Hits, stored.HitsMax, stored.Stamina, stored.StaminaMax, stored.Mana, stored.ManaMax));
        Assert.Equal(
            [(SkillType.Alchemy, 500), (SkillType.Magery, 500)],
            stored.Skills.Select(skill => (skill.Skill, skill.Base)).OrderBy(skill => skill.Skill)
        );
        Assert.Equal((new Point3D(4408, 1168, 0), MapType.Felucca), (stored.Location, stored.Map));
        Assert.Null(stored.TemplateId);
        Assert.Equal(NotorietyType.Innocent, stored.Notoriety);

        var items = await _items.QueryAsync(item => item.MobileId == stored.Id);
        var backpack = Assert.Single(items);
        var packed = await _items.QueryAsync(item => item.ContainerId == backpack.Id);
        Assert.Equal(["bottle", "gold"], packed.Select(item => item.TemplateId).Order());

        var evt = Assert.Single(_created);
        Assert.Equal(stored.Id, evt.Character.Id);
        Assert.Equal(3, evt.Items.Count);
    }

    [Fact]
    public async Task CreateAsync_Profession_UsesItsStatsAndSkills()
    {
        var service = CreateService();

        await service.CreateAsync(
            Account,
            Request() with { Profession = 2, Strength = 0, Dexterity = 0, Intelligence = 0, Skills = [] }
        );

        var stored = Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Equal((45, 35, 10), (stored.Strength, stored.Dexterity, stored.Intelligence));
        Assert.Equal(
            [(SkillType.Anatomy, 300), (SkillType.Healing, 450), (SkillType.Tactics, 500), (SkillType.Swordsmanship, 350)],
            stored.Skills.Select(skill => (skill.Skill, skill.Base)).OrderBy(skill => skill.Skill)
        );
    }

    [Fact]
    public async Task CreateAsync_UnknownProfession_UsesThePlayersChoice()
    {
        var service = CreateService();

        await service.CreateAsync(Account, Request() with { Profession = 250 });

        var stored = Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Equal((60, 20, 10), (stored.Strength, stored.Dexterity, stored.Intelligence));
    }

    [Fact]
    public async Task CreateAsync_InvalidChoices_AreSanitized()
    {
        var service = CreateService();

        await service.CreateAsync(
            Account,
            Request() with { Name = "x", Strength = 90, Dexterity = 0, Intelligence = 0, StartingCity = 99, HairStyle = 0x9999 }
        );

        var stored = Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Equal("Generic Player", stored.Name);
        Assert.Equal((10, 10, 10), (stored.Strength, stored.Dexterity, stored.Intelligence));
        Assert.Equal(new Point3D(1496, 1628, 10), stored.Location);
        Assert.Equal(0, stored.HairStyle);
    }

    [Fact]
    public async Task CreateAsync_AccountAtTheLimit_IsRefused()
    {
        var service = CreateService(maxPerAccount: 1);
        await service.CreateAsync(Account, Request());

        var result = await service.CreateAsync(Account, Request() with { Slot = 0, Name = "Bran" });

        Assert.Equal(CharacterCreationRefusalType.TooManyCharacters, result.Refusal);
        Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Single(_created);
    }

    [Theory, InlineData(0), InlineData(7), InlineData(-1)]
    public async Task CreateAsync_SlotInUseOrOutOfRange_IsRefused(int slot)
    {
        var service = CreateService();
        await service.CreateAsync(Account, Request());

        var result = await service.CreateAsync(Account, Request() with { Slot = slot, Name = "Bran" });

        Assert.Equal(CharacterCreationRefusalType.SlotUnavailable, result.Refusal);
        Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
    }

    [Fact]
    public async Task CreateAsync_SlotTakenByAConcurrentCreate_IsRefused()
    {
        var service = CreateService();

        var results = await Task.WhenAll(
            service.CreateAsync(Account, Request() with { Slot = 3 }),
            service.CreateAsync(Account, Request() with { Slot = 3, Name = "Bran" })
        );

        Assert.Single(results, result => result.IsCreated);
        Assert.Single(results, result => result.Refusal == CharacterCreationRefusalType.SlotUnavailable);
        var stored = Assert.Single(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Single(await _items.QueryAsync(item => item.MobileId == stored.Id));
        Assert.Single(_created);
    }

    [Fact]
    public async Task CreateAsync_StartingItemsFail_RollsBackTheCharacter()
    {
        // Gold on a template that does not stack makes the item factory throw after the mobile was inserted.
        var service = CreateService(goldTemplate: "shirt");

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Account, Request()));

        Assert.Empty(await _mobiles.QueryAsync(mobile => mobile.AccountId == Account));
        Assert.Empty(_created);
    }

    [Fact]
    public async Task GetCharactersAsync_ReturnsOnlyThatAccountsPlayers_BySlot()
    {
        var service = CreateService();
        await service.CreateAsync(Account, Request() with { Slot = 4, Name = "Bran" });
        await service.CreateAsync(Account, Request() with { Slot = 1, Name = "Aria" });
        await service.CreateAsync(new Serial(0x999), Request() with { Name = "Other" });

        var characters = await service.GetCharactersAsync(Account);

        Assert.Equal(["Aria", "Bran"], characters.Select(character => character.Name));
    }

    private CharacterService CreateService(int maxPerAccount = 7, string goldTemplate = "gold")
    {
        var loaders = new StubDataLoaderService()
                      .With(
                          Template("backpack", 0x0E75),
                          Template("gold", 0x0EED),
                          Template("bottle", 0x0F0E),
                          Template("shirt", 0x1517)
                      )
                      .With(new ContainerContent { Name = "default", Bounds = new(new Point2D(44, 65), new Point2D(186, 159)), Default = true })
                      .With(new StartingItemSet { Common = true, Items = [new StartingItemEntry { Items = ["bottle"] }] })
                      .With(
                          new RaceContent
                          {
                              Race = RaceType.Human, Name = "Human",
                              SkinHues = [HueSpec.FromRange(0x3EA, 0x422)], HairHues = [HueSpec.FromRange(0x44E, 0x47D)],
                              Male = new RaceGenderContent { Body = 400, Hair = [0x203B], Beard = [0x203E] },
                              Female = new RaceGenderContent { Body = 401, Hair = [0x203C], Beard = [] }
                          }
                      )
                      .With(
                          new StartingCityContent { Town = "Britain", Description = "", Location = new(1496, 1628, 10), Map = MapType.Felucca },
                          new StartingCityContent { Town = "Moonglow", Description = "", Location = new(4408, 1168, 0), Map = MapType.Felucca }
                      )
                      .With(
                          new ProfessionContent
                          {
                              Id = 2, Name = "Warrior", Str = 45, Dex = 35, Int = 10,
                              Skills =
                              [
                                  new() { Skill = SkillType.Anatomy, Value = 30 }, new() { Skill = SkillType.Healing, Value = 45 },
                                  new() { Skill = SkillType.Swordsmanship, Value = 35 }, new() { Skill = SkillType.Tactics, Value = 50 }
                              ]
                          }
                      )
                      .With(new BannedNamesContent());
        var tiles = new FakeTileDataService()
                    .Item(0x0E75, TileFlagType.Container, 0, layer: (byte)LayerType.Backpack)
                    .Item(0x0EED, TileFlagType.Generic, 0)
                    .Item(0x0F0E, TileFlagType.None, 0)
                    .Item(0x1517, TileFlagType.Wearable, 0, layer: (byte)LayerType.Shirt);
        var templates = new ItemTemplateService(loaders);
        var startingItems = new StartingItemsService(
            loaders,
            new ItemFactoryService(templates, tiles, _host.Owner),
            templates,
            new ContainerLayoutService(loaders),
            tiles,
            _host.Owner,
            new StartingItemsConfig(),
            new ItemsConfig { BackpackTemplate = "backpack", GoldTemplate = goldTemplate }
        );

        return new CharacterService(
            loaders,
            startingItems,
            _host.Owner,
            _eventContainer.Resolve<IMoongateEventBus>(),
            new CharactersConfig { MaxPerAccount = maxPerAccount }
        );
    }

    private static CharacterCreationRequest Request()
    {
        return new()
        {
            Slot = 0,
            Name = "Aria",
            Profession = 0,
            StartingCity = 0,
            Gender = GenderType.Female,
            Race = RaceType.Human,
            Strength = 60,
            Dexterity = 20,
            Intelligence = 10,
            Skills = [new() { Skill = SkillType.Alchemy, Value = 50 }, new() { Skill = SkillType.Magery, Value = 50 }],
            SkinHue = new(0x3F0),
            HairStyle = 0x203C,
            HairHue = new(0x450),
            BeardStyle = 0,
            BeardHue = new(0),
            ShirtHue = new(0x20),
            PantsHue = new(0x30)
        };
    }

    private static ItemTemplate Template(string id, int itemId)
    {
        return new() { Id = id, ItemId = new Serial((uint)itemId), Hue = HueSpec.FromValue(0) };
    }
}
