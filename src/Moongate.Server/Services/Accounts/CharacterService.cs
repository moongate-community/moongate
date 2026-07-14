using System.Globalization;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Interfaces.World;
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
        IEventBus eventBus
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

        var backpack = EquipContainer(mobile, BackpackTemplateId, LayerType.Backpack);
        EquipContainer(mobile, BankBoxTemplateId, LayerType.Bank);

        if (backpack is not null)
        {
            GiveStartingItems(mobile, backpack, packet);
        }

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
