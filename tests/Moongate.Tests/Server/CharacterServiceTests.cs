using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
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
    public async Task DeleteCharacter_RemovesTheMobileEverythingItOwnsAndTheAccountLink()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var service = Service(persistence, new());
        var mobile = service.CreateCharacter(accountId, Packet());

        // Everything the character owns before the deletion: containers plus their contents.
        var backpackId = mobile.BackpackId;
        var ownedIds = persistence.Store<ItemEntity>().Query().Select(item => item.Id).ToList();
        Assert.NotEmpty(persistence.Store<ItemEntity>().GetById(backpackId)!.ContainedItemIds);

        var result = service.DeleteCharacter(accountId, 0);

        Assert.Null(result); // null means deleted
        Assert.Null(persistence.Store<MobileEntity>().GetById(mobile.Id));

        // No orphans: the backpack, the bank box and every item inside them are gone with the mobile.
        Assert.All(ownedIds, id => Assert.Null(persistence.Store<ItemEntity>().GetById(id)));
        Assert.Empty(persistence.Store<ItemEntity>().Query());

        var account = persistence.Store<AccountEntity>().GetById(accountId);
        Assert.DoesNotContain(mobile.Id, account!.MobileIds);
        Assert.Empty(service.GetPlayerCharacters(accountId));
    }

    [Fact]
    public async Task DeleteCharacter_PublishesCharacterDeletedEventCarryingTheGoneMobile()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var eventBus = new EventBusService();
        CharacterDeletedEvent? published = null;
        eventBus.Subscribe<CharacterDeletedEvent>((evt, _) =>
            {
                published = evt;

                return Task.CompletedTask;
            }
        );

        var service = Service(persistence, eventBus);
        var mobile = service.CreateCharacter(accountId, Packet());

        service.DeleteCharacter(accountId, 0);

        Assert.NotNull(published);
        Assert.Equal(accountId, published!.AccountId);
        Assert.Equal(mobile.Id, published.MobileId);

        // The mobile is gone from the store, so the event is the only way left to read what it was.
        Assert.Same(mobile, published.Character);
        Assert.Equal("Freydis", published.Character.Name);
        Assert.Null(persistence.Store<MobileEntity>().GetById(mobile.Id));
    }

    [Fact]
    public async Task DeleteCharacter_RefusedDeletion_PublishesNothing()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var eventBus = new EventBusService();
        var published = 0;
        eventBus.Subscribe<CharacterDeletedEvent>((_, _) =>
            {
                published++;

                return Task.CompletedTask;
            }
        );

        var service = Service(persistence, eventBus);
        service.CreateCharacter(accountId, Packet());

        service.DeleteCharacter(accountId, 42);

        Assert.Equal(0, published);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)] // only slot 0 exists
    [InlineData(99)]
    public async Task DeleteCharacter_SlotOutOfRange_IsRefusedAndChangesNothing(int slot)
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var service = Service(persistence, new());
        var mobile = service.CreateCharacter(accountId, Packet());

        Assert.Equal(DeleteResultType.BadRequest, service.DeleteCharacter(accountId, slot));
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(mobile.Id));
        Assert.Single(service.GetPlayerCharacters(accountId));
    }

    [Fact]
    public async Task DeleteCharacter_LeavesTheOtherCharactersAlone()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var service = Service(persistence, new());
        var first = service.CreateCharacter(accountId, Packet());
        var second = service.CreateCharacter(accountId, Packet());

        Assert.Null(service.DeleteCharacter(accountId, 0));

        var remaining = Assert.Single(service.GetPlayerCharacters(accountId));
        Assert.Equal(second.Id, remaining.Id);

        // The survivor kept its own gear.
        Assert.NotNull(persistence.Store<ItemEntity>().GetById(second.BackpackId));
        Assert.Null(persistence.Store<ItemEntity>().GetById(first.BackpackId));
    }

    [Fact]
    public async Task CreateCharacter_PersistsMobileWithAllocatedSerialAndLinksItToTheAccount()
    {
        var persistence = new FakePersistenceService();

        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var eventBus = new EventBusService();
        CharacterCreatedEvent? published = null;
        eventBus.Subscribe<CharacterCreatedEvent>((evt, _) =>
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
    public void CreateCharacter_PublishesCharacterReadyEventWithContainerSerials()
    {
        var persistence = new FakePersistenceService();
        var eventBus = new EventBusService();

        CharacterReadyEvent? ready = null;
        eventBus.Subscribe<CharacterReadyEvent>((evt, _) =>
            {
                ready = evt;

                return Task.CompletedTask;
            }
        );

        var service = Service(persistence, eventBus);

        var mobile = service.CreateCharacter((Serial)5, Packet());

        Assert.NotNull(ready);
        Assert.Equal((Serial)5, ready!.AccountId);
        Assert.Same(mobile, ready.Character);
        Assert.Equal(mobile.BackpackId, ready.BackpackId);
        Assert.NotEqual(Serial.Zero, ready.BackpackId);
        Assert.Equal(mobile.EquippedItemIds[LayerType.Bank], ready.BankId);
        Assert.NotEqual(Serial.Zero, ready.BankId);
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
        => CharacterServiceFixture.Create(persistence, eventBus);



}
