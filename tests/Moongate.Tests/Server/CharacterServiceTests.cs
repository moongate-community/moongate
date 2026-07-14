using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server;

public class CharacterServiceTests
{
    [Fact]
    public void CreateCharacter_GivesAndEquipsABackpack()
    {
        var persistence = new FakePersistenceService();
        var service = Service(persistence, new());

        var mobile = service.CreateCharacter((Serial)5, Packet());

        // The mobile has a backpack serial, equipped on the Backpack layer.
        Assert.NotEqual(Serial.Zero, mobile.BackpackId);
        Assert.Equal(mobile.BackpackId, mobile.EquippedItemIds[LayerType.Backpack]);

        // The backpack item is persisted and back-references the mobile.
        var backpack = persistence.Store<ItemEntity>().GetById(mobile.BackpackId);
        Assert.NotNull(backpack);
        Assert.Equal(3701, backpack!.ItemId);
        Assert.Equal(mobile.Id, backpack.EquippedMobileId);
        Assert.Equal(LayerType.Backpack, backpack.EquippedLayer);
    }

    [Fact]
    public void CreateCharacter_GivesAndEquipsABankBox()
    {
        var persistence = new FakePersistenceService();
        var service = Service(persistence, new());

        var mobile = service.CreateCharacter((Serial)5, Packet());

        // The bank box is equipped on the Bank layer (no dedicated field on the mobile).
        var bankBoxId = mobile.EquippedItemIds[LayerType.Bank];
        Assert.NotEqual(Serial.Zero, bankBoxId);

        var bankBox = persistence.Store<ItemEntity>().GetById(bankBoxId);
        Assert.NotNull(bankBox);
        Assert.Equal(2475, bankBox!.ItemId);
        Assert.Equal(mobile.Id, bankBox.EquippedMobileId);
        Assert.Equal(LayerType.Bank, bankBox.EquippedLayer);
    }

    [Fact]
    public void CreateCharacter_GrantsResolvedStartingKit()
    {
        var persistence = new FakePersistenceService();
        var service = Service(persistence, new());

        // Packet: Elf/Female -> ByBody equips the shirt; All -> dagger in the backpack.
        var mobile = service.CreateCharacter((Serial)5, Packet());

        var itemService = new ItemService(persistence);

        var contents = itemService.GetContents(mobile.BackpackId);
        Assert.Contains(contents, item => item.ItemId == 3921); // dagger from All.Pack

        var equipped = itemService.GetEquipped(mobile);
        var shirt = Assert.Single(equipped.Where(item => item.ItemId == 5399)); // shirt from ByBody Elf/Female
        Assert.Equal(LayerType.Shirt, shirt.EquippedLayer);
        Assert.Equal((ushort)0x0765, shirt.Hue.Value); // Hue "shirt" resolved to packet.ShirtHue
    }

    [Fact]
    public async Task CreateCharacter_PersistsMobileWithAllocatedSerialAndLinksItToTheAccount()
    {
        var persistence = new FakePersistenceService();

        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var eventBus = new EventBusService();
        CharacterCreatedEvent? published = null;
        eventBus.Subscribe<CharacterCreatedEvent>(
            (evt, _) =>
            {
                published = evt;

                return Task.CompletedTask;
            }
        );

        var service = Service(persistence, eventBus);

        var mobile = service.CreateCharacter(accountId, Packet());

        // The store allocated a non-zero serial and wrote it back onto the returned mobile.
        Assert.NotEqual(Serial.Zero, mobile.Id);

        // The mobile is persisted under that serial.
        var stored = persistence.Store<MobileEntity>().GetById(mobile.Id);
        Assert.NotNull(stored);
        Assert.Equal("Freydis", stored!.Name);

        // The account now references the new mobile.
        var account = persistence.Store<AccountEntity>().GetById(accountId);
        Assert.NotNull(account);
        Assert.Contains(mobile.Id, account!.MobileIds);

        // A CharacterCreatedEvent was published carrying the account, the mobile serial and the mobile.
        Assert.NotNull(published);
        Assert.Equal(accountId, published!.AccountId);
        Assert.Equal(mobile.Id, published.MobileId);
        Assert.Same(mobile, published.Character);
    }

    [Fact]
    public void CreateCharacter_UnknownAccount_StillPersistsMobileWithoutLinking()
    {
        var persistence = new FakePersistenceService();
        var service = Service(persistence, new());

        var mobile = service.CreateCharacter((Serial)999, Packet());

        Assert.NotEqual(Serial.Zero, mobile.Id);
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(mobile.Id));
    }

    private static StartingCityService Cities()
    {
        var service = new StartingCityService();
        service.Register(
            new()
            {
                City = "Britain", Building = "Inn", Description = 1, X = 1602, Y = 1591, Z = 20, Map = MapType.Trammel
            }
        );

        return service;
    }

    private static CharacterCreationPacket Packet()
        => new(
            0,
            "Freydis",
            0,
            4,
            GenderType.Female,
            RaceType.Elf,
            45,
            20,
            25,
            [new(1, 50)],
            0x03EA,
            0x203C,
            0x044E,
            0x2040,
            0x0450,
            0,
            0x0765,
            0x0766
        );

    private static CharacterService Service(FakePersistenceService persistence, EventBusService eventBus)
    {
        var templates = Templates();
        var random = new Random(1);
        var factory = new ItemFactoryService(templates, random);

        return new(
            persistence,
            new MobileFactoryService(Cities()),
            factory,
            new ItemService(persistence),
            templates,
            StartingItems(),
            Skills(),
            random,
            eventBus
        );
    }

    private static SkillService Skills()
    {
        var service = new SkillService();
        service.Register(new() { Id = 1, Name = "Anatomy" });

        return service;
    }

    private static StartingItemsService StartingItems()
    {
        var service = new StartingItemsService();
        service.Load(
            new()
            {
                All = new() { Pack = [new() { Item = "dagger" }] },
                ByBody =
                {
                    ["Elf/Female"] = new() { Equip = [new() { Item = "shirt", Hue = "shirt" }] }
                }
            }
        );

        return service;
    }

    private static ItemTemplateService Templates()
    {
        var templates = new ItemTemplateService();
        templates.Register(new() { Id = "backpack", Name = "Backpack", Category = "Containers", ItemId = 3701 });
        templates.Register(new() { Id = "bank_box", Name = "Bank Box", Category = "Containers", ItemId = 2475 });
        templates.Register(new() { Id = "dagger", Name = "Dagger", Category = "Weapons", ItemId = 3921 });
        templates.Register(
            new()
            {
                Id = "shirt", Name = "Shirt", Category = "Clothing", ItemId = 5399,
                Equip = new() { Layer = LayerType.Shirt }
            }
        );

        return templates;
    }
}
