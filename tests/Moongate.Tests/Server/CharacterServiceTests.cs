using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.Items;
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
    public void CreateCharacter_OffLoopStrict_Throws()
    {
        var persistence = new FakePersistenceService();
        var loopAffinity = new LoopAffinity(
            new StubLoopThread(onLoop: false),
            new MoongateConfig { StrictLoopAffinity = true }
        );
        var service = CharacterServiceFixture.Create(persistence, new EventBusService(), loopAffinity: loopAffinity);

        Assert.Throws<InvalidOperationException>(() => service.CreateCharacter((Serial)5, Packet()));
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
    public async Task DeleteCharacter_OffLoopStrict_Throws()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var service = CharacterServiceFixture.Create(persistence, new EventBusService());
        var mobile = service.CreateCharacter(accountId, Packet());

        var loopAffinity = new LoopAffinity(
            new StubLoopThread(onLoop: false),
            new MoongateConfig { StrictLoopAffinity = true }
        );
        var guardedService = CharacterServiceFixture.Create(
            persistence,
            new EventBusService(),
            loopAffinity: loopAffinity
        );

        Assert.Throws<InvalidOperationException>(() => guardedService.DeleteCharacter(accountId, 0));

        // Refused before anything was touched: the mobile created above is still there.
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(mobile.Id));
    }

    [Fact]
    public async Task DeleteCharacter_PlayingADifferentCharacter_StillDeletesThisOne()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var sessions = new StubSessionManager();
        var service = Service(persistence, new(), sessions);
        var mobile = service.CreateCharacter(accountId, Packet());

        // A session is busy with someone else entirely — that must not protect this character.
        sessions.Played.Add((Serial)0x7777);

        Assert.Null(service.DeleteCharacter(accountId, 0));
        Assert.Null(persistence.Store<MobileEntity>().GetById(mobile.Id));
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

    [Theory, InlineData(-1), InlineData(1), InlineData(99)]

    // only slot 0 exists
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
    public async Task DeleteCharacter_SomebodyIsPlayingIt_IsRefusedAndChangesNothing()
    {
        var persistence = new FakePersistenceService();
        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "bob" });

        var sessions = new StubSessionManager();
        var service = Service(persistence, new(), sessions);
        var mobile = service.CreateCharacter(accountId, Packet());

        // Another session has this very character in hand.
        sessions.Played.Add(mobile.Id);

        Assert.Equal(DeleteResultType.CharBeingPlayed, service.DeleteCharacter(accountId, 0));

        // The mobile survives, and so does the account link.
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(mobile.Id));
        Assert.Contains(mobile.Id, persistence.Store<AccountEntity>().GetById(accountId)!.MobileIds);
    }

    [Fact]
    public async Task GetPlayerCharacters_ReturnsOnlyTheAccountsOwnCharacters()
    {
        // Characterises the behaviour before the read path is rewritten: the account's own characters, and
        // nothing else — not another account's, and not a mobile belonging to nobody, which is what every
        // NPC on a real shard looks like in this store.
        var persistence = new FakePersistenceService();
        var mine = (Serial)5;
        var theirs = (Serial)6;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = mine, Username = "bob" });
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = theirs, Username = "alice" });

        var service = Service(persistence, new());
        var myCharacter = service.CreateCharacter(mine, Packet());
        service.CreateCharacter(theirs, Packet());
        await persistence.Store<MobileEntity>().UpsertAsync(new() { Name = "a wandering healer" });

        var characters = service.GetPlayerCharacters(mine);

        Assert.Equal(myCharacter.Id, Assert.Single(characters).Id);
    }

    [Fact]
    public void GetPlayerCharacters_UnknownAccount_IsEmpty()
    {
        var service = Service(new(), new());

        Assert.Empty(service.GetPlayerCharacters(new(0xDEADBEEF)));
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

    private static CharacterService Service(
        FakePersistenceService persistence,
        EventBusService eventBus,
        ISessionManager? sessions = null
    )
        => CharacterServiceFixture.Create(persistence, eventBus, sessions);
}
