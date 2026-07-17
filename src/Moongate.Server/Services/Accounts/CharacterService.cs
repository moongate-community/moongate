using System.Globalization;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.StartingItems;
using Serilog;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Accounts;

public class CharacterService : ICharacterService
{
    private const string BackpackTemplateId = "backpack";
    private const string BankBoxTemplateId = "bank_box";

    private readonly IEntityStore<MobileEntity, Serial> _mobileStore;
    private readonly IEntityStore<AccountEntity, Serial> _accountStore;
    private readonly IMobileFactoryService _mobileFactory;
    private readonly IItemFactoryService _itemFactory;
    private readonly IItemService _itemService;
    private readonly IItemTemplateService _templates;
    private readonly IStartingItemsService _startingItems;
    private readonly ISkillService _skills;
    private readonly Random _random;
    private readonly IEventBus _eventBus;
    private readonly ISessionManager _sessions;

    private readonly ILogger _logger = Log.ForContext<CharacterService>();

    public CharacterService(
        IPersistenceService persistenceService,
        IMobileFactoryService mobileFactory,
        IItemFactoryService itemFactory,
        IItemService itemService,
        IItemTemplateService templates,
        IStartingItemsService startingItems,
        ISkillService skills,
        Random random,
        IEventBus eventBus,
        ISessionManager sessions
    )
    {
        _mobileStore = persistenceService.GetStore<MobileEntity, Serial>();
        _accountStore = persistenceService.GetStore<AccountEntity, Serial>();
        _mobileFactory = mobileFactory;
        _itemFactory = itemFactory;
        _itemService = itemService;
        _templates = templates;
        _startingItems = startingItems;
        _skills = skills;
        _random = random;
        _eventBus = eventBus;
        _sessions = sessions;
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

        _eventBus.Publish(new CharacterCreatedEvent(accountId, mobile.Id, mobile));

        var backpack = EquipContainer(mobile, BackpackTemplateId, LayerType.Backpack);
        var bank = EquipContainer(mobile, BankBoxTemplateId, LayerType.Bank);

        if (backpack is not null)
        {
            GiveStartingItems(mobile, backpack, packet);
        }

        _eventBus.Publish(
            new CharacterReadyEvent(
                accountId,
                mobile,
                backpack?.Id ?? Serial.Zero,
                bank?.Id ?? Serial.Zero
            )
        );

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

    public DeleteResultType? DeleteCharacter(Serial accountId, int slot)
    {
        var characters = GetPlayerCharacters(accountId).ToList();

        if (slot < 0 || slot >= characters.Count)
        {
            return DeleteResultType.BadRequest;
        }

        var mobile = characters[slot];

        // Someone is playing this character right now — deleting it would pull the world out from under
        // them. ModernUO asks the same question as Mobile.NetState != null.
        if (_sessions.IsCharacterPlayed(mobile.Id))
        {
            return DeleteResultType.CharBeingPlayed;
        }

        // Everything the character owns goes with it: the mobile store is the only thing linking the
        // backpack, the bank box and their contents, so dropping it alone would strand them.
        foreach (var item in _itemService.GetEquipped(mobile))
        {
            DeleteItemTree(item);
        }

        _mobileStore.RemoveAsync(mobile.Id).WaitSync();

        var account = _accountStore.GetById(accountId);

        if (account is not null)
        {
            account.MobileIds.Remove(mobile.Id);
            _accountStore.UpsertAsync(account).WaitSync();
        }

        _eventBus.Publish(new CharacterDeletedEvent(accountId, mobile.Id, mobile));

        _logger.Information(
            "Character deleted for account {AccountId}: {Name} (serial {Serial}) from slot {Slot}",
            accountId,
            mobile.Name,
            mobile.Id,
            slot
        );

        return null;
    }

    public IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId)
    {
        // Reads the account's own id list rather than filtering the mobile store by it. The store's Query()
        // deep-clones every entity it holds before any Where runs, so the old shape cloned every account
        // and every mobile — NPCs included — to return a handful of characters. GetById is a dictionary
        // lookup and one clone, and an account holds at most a few characters.
        var account = _accountStore.GetById(accountId);

        if (account == null)
        {
            _logger.Warning("Account not found: {AccountId}", accountId);

            return [];
        }

        var characters = new List<MobileEntity>(account.MobileIds.Count);

        foreach (var id in account.MobileIds)
        {
            // A dangling id is not a failure worth refusing the whole list for: the character is gone and
            // the account's list has not caught up. Skipping is what the old filter did implicitly.
            if (_mobileStore.GetById(id) is { } mobile)
            {
                characters.Add(mobile);
            }
        }

        return characters;
    }

    // Depth-first so a container's contents are gone before the container itself.
    private void DeleteItemTree(ItemEntity item)
    {
        foreach (var contained in _itemService.GetContents(item.Id))
        {
            DeleteItemTree(contained);
        }

        _itemService.Delete(item.Id);
    }

    private ItemEntity? EquipContainer(MobileEntity mobile, string templateId, LayerType layer)
    {
        var containers = _itemFactory.CreateFromTemplate(templateId);

        if (containers.Count == 0)
        {
            _logger.Warning(
                "Container template '{TemplateId}' not found; {Name} created without it",
                templateId,
                mobile.Name
            );

            return null;
        }

        _itemService.Equip(mobile, containers[0], layer);

        return containers[0];
    }

    private void GiveStartingItems(MobileEntity mobile, ItemEntity backpack, CharacterCreationPacket packet)
    {
        var topSkillNames = packet.Skills
            .Where(skill => skill.Value > 0)
            .OrderByDescending(skill => skill.Value)
            .Take(3)
            .Select(skill => _skills.GetById(skill.SkillId)?.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();

        var kit = _startingItems.Resolve(packet.Race, packet.Gender, topSkillNames);

        foreach (var entry in kit.Equip)
        {
            var template = _templates.GetById(entry.Item);

            if (template?.Equip is null)
            {
                _logger.Warning("Starting equip '{Item}' missing template or layer; skipped", entry.Item);

                continue;
            }

            var items = _itemFactory.CreateFromTemplate(entry.Item, amount: entry.Amount, hue: ResolveHue(entry, packet));

            if (items.Count > 0)
            {
                _itemService.Equip(mobile, items[0], template.Equip.Layer);
            }
        }

        foreach (var entry in kit.Pack)
        {
            var items = _itemFactory.CreateFromTemplate(entry.Item, amount: entry.Amount, hue: ResolveHue(entry, packet));

            if (items.Count == 0)
            {
                _logger.Warning("Starting pack item '{Item}' missing template; skipped", entry.Item);

                continue;
            }

            _itemService.AddToContainer(backpack, items[0], RandomBackpackPosition());
        }
    }

    private Point2D RandomBackpackPosition()
        => new(_random.Next(44, 140), _random.Next(65, 140));

    private Hue? ResolveHue(StartingItemEntry entry, CharacterCreationPacket packet)
    {
        if (string.IsNullOrEmpty(entry.Hue))
        {
            return null;
        }

        if (entry.Hue.Equals("shirt", StringComparison.OrdinalIgnoreCase))
        {
            return new Hue((ushort)packet.ShirtHue);
        }

        if (entry.Hue.Equals("pants", StringComparison.OrdinalIgnoreCase))
        {
            return new Hue((ushort)packet.PantsHue);
        }

        var text = entry.Hue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? entry.Hue[2..] : entry.Hue;

        return ushort.TryParse(text, NumberStyles.HexNumber, null, out var value) ? new Hue(value) : null;
    }
}
