using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Ultima.Types;
using Serilog;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Accounts;

public class CharacterService : ICharacterService
{
    private const string BackpackTemplateId = "backpack";

    private readonly IEntityStore<MobileEntity, Serial> _mobileStore;
    private readonly IEntityStore<AccountEntity, Serial> _accountStore;
    private readonly IMobileFactoryService _mobileFactory;
    private readonly IItemFactoryService _itemFactory;
    private readonly IItemService _itemService;
    private readonly IEventBus _eventBus;

    private readonly ILogger _logger =  Log.ForContext<CharacterService>();


    public CharacterService(
        IPersistenceService persistenceService,
        IMobileFactoryService mobileFactory,
        IItemFactoryService itemFactory,
        IItemService itemService,
        IEventBus eventBus
    )
    {
        _mobileStore = persistenceService.GetStore<MobileEntity, Serial>();
        _accountStore = persistenceService.GetStore<AccountEntity, Serial>();
        _mobileFactory = mobileFactory;
        _itemFactory = itemFactory;
        _itemService = itemService;
        _eventBus = eventBus;
    }

    public IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId)
    {
        var account = _accountStore.Query().FirstOrDefault(a => a.Id == accountId);

        if (account == null)
        {
            _logger.Warning("Account not found: {AccountId}", accountId);
            return [];
        }

        return [.. _mobileStore.Query().Where(mobile => account.MobileIds.Contains(mobile.Id))];
    }

    public MobileEntity CreateCharacter(Serial accountId, CharacterCreationPacket packet)
    {
        var mobile = _mobileFactory.CreatePlayerMobile(packet);

        // Upsert with a default serial: the store allocates the next mobile serial and writes it back
        // onto the instance, so mobile.Id is populated before we link it to the account.
        _mobileStore.UpsertAsync(mobile).WaitSync();

        var account = _accountStore.GetById(accountId);

        if (account is not null)
        {
            account.MobileIds.Add(mobile.Id);
            _accountStore.UpsertAsync(account).WaitSync();
        }

        GiveBackpack(mobile);

        _eventBus.Publish(new CharacterCreatedEvent(accountId, mobile.Id, mobile));

        _logger.Information(
            "Character created for account {AccountId}: {Name} (serial {Serial}) gender {Gender} race {Race} " +
            "at map {MapId} {Position}",
            accountId,
            mobile.Name,
            mobile.Id,
            mobile.Gender,
            mobile.Race,
            mobile.MapId,
            mobile.Position
        );

        return mobile;
    }

    private void GiveBackpack(MobileEntity mobile)
    {
        var backpacks = _itemFactory.CreateFromTemplate(BackpackTemplateId);

        if (backpacks.Count == 0)
        {
            _logger.Warning("Backpack template '{TemplateId}' not found; {Name} created without a backpack", BackpackTemplateId, mobile.Name);

            return;
        }

        _itemService.Equip(mobile, backpacks[0], LayerType.Backpack);
    }
}
