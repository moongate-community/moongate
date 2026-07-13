using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.StartingCities;
using Moongate.UO.Data.Types;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server;

public class CharacterServiceTests
{
    private static CharacterCreationPacket Packet()
    {
        return new CharacterCreationPacket(
            Slot: 0,
            Name: "Freydis",
            ClientFlags: 0,
            ProfessionId: 4,
            Gender: GenderType.Female,
            Race: RaceType.Elf,
            Strength: 45,
            Dexterity: 20,
            Intelligence: 25,
            Skills: [new CharacterSkill(1, 50)],
            SkinHue: 0x03EA,
            HairStyle: 0x203C,
            HairHue: 0x044E,
            FacialHairStyle: 0x2040,
            FacialHairHue: 0x0450,
            StartingCityIndex: 0,
            ShirtHue: 0x0765,
            PantsHue: 0x0766
        );
    }

    private static StartingCityService Cities()
    {
        var service = new StartingCityService();
        service.Register(new StartingCity
        {
            City = "Britain", Building = "Inn", Description = 1, X = 1602, Y = 1591, Z = 20, Map = MapType.Trammel
        });
        return service;
    }

    [Fact]
    public async Task CreateCharacter_PersistsMobileWithAllocatedSerialAndLinksItToTheAccount()
    {
        var persistence = new FakePersistenceService();

        var accountId = (Serial)5;
        await persistence.Store<AccountEntity>().UpsertAsync(new AccountEntity { Id = accountId, Username = "bob" });

        var eventBus = new EventBusService();
        CharacterCreatedEvent? published = null;
        eventBus.Subscribe<CharacterCreatedEvent>((evt, _) =>
        {
            published = evt;
            return Task.CompletedTask;
        });

        var factory = new MobileFactoryService(Cities());
        var service = new CharacterService(persistence, factory, eventBus);

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
        var service = new CharacterService(persistence, new MobileFactoryService(Cities()), new EventBusService());

        var mobile = service.CreateCharacter((Serial)999, Packet());

        Assert.NotEqual(Serial.Zero, mobile.Id);
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(mobile.Id));
    }
}
