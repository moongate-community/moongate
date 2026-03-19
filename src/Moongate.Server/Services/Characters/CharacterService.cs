using Moongate.Server.Data.Entities;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Characters;

/// <summary>
/// Represents CharacterService.
/// </summary>
public class CharacterService : ICharacterService
{
    private const int StarterGoldQuantity = 1000;

    private readonly ILogger _logger = Log.ForContext<CharacterService>();
    private readonly IPersistenceService _persistenceService;
    private readonly IEntityFactoryService _entityFactoryService;
    private readonly IGameEventBusService _gameEventBusService;

    public CharacterService(
        IPersistenceService persistenceService,
        IEntityFactoryService entityFactoryService,
        IGameEventBusService gameEventBusService
    )
    {
        _persistenceService = persistenceService;
        _entityFactoryService = entityFactoryService;
        _gameEventBusService = gameEventBusService;
    }

    public async Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
    {
        var account = await _persistenceService.UnitOfWork.Accounts.GetByIdAsync(accountId);

        if (account == null)
        {
            _logger.Warning(
                "Cannot add character {CharacterId} to account {AccountId}: account not found",
                characterId,
                accountId
            );

            return false;
        }

        if (account.CharacterIds.Contains(characterId))
        {
            _logger.Warning(
                "Cannot add character {CharacterId} to account {AccountId}: character already linked",
                characterId,
                accountId
            );

            return false;
        }

        account.CharacterIds.Add(characterId);
        await _persistenceService.UnitOfWork.Accounts.UpsertAsync(account);

        _logger.Information("Added character {CharacterId} to account {AccountId}", characterId, accountId);

        return true;
    }

    public async Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
    {
        var character = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(characterId);

        if (character is null)
        {
            return;
        }

        await ApplyEquippedItemHueAsync(character, ItemLayerType.Shirt, shirtHue);
        await ApplyEquippedItemHueAsync(character, ItemLayerType.Pants, pantsHue);
    }

    public async Task<Serial> CreateCharacterAsync(UOMobileEntity character)
    {
        character.Id = _persistenceService.UnitOfWork.AllocateNextMobileId();
        await EnsureStarterInventoryAsync(character);

        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(character);

        await _gameEventBusService.PublishAsync(
            new CharacterCreatedEvent(
                character.Name,
                character.AccountId,
                character.Id,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            )
        );

        _logger.Debug("Created character {CharacterName}", character.Name);

        return character.Id;
    }

    public async Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
    {
        ArgumentNullException.ThrowIfNull(character);

        var backpackId = character.BackpackId;

        if (backpackId == Serial.Zero &&
            character.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId))
        {
            backpackId = equippedBackpackId;
        }

        if (backpackId == Serial.Zero)
        {
            return null;
        }

        var backpack = await _persistenceService.UnitOfWork.Items.GetByIdAsync(backpackId);

        if (backpack is null)
        {
            return null;
        }

        var hydratedBackpack = CloneItem(backpack);
        await HydrateContainedItemsRecursiveAsync(hydratedBackpack);

        return hydratedBackpack;
    }

    public async Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!character.EquippedItemIds.TryGetValue(ItemLayerType.Bank, out var bankBoxId) || bankBoxId == Serial.Zero)
        {
            return null;
        }

        var bankBox = await _persistenceService.UnitOfWork.Items.GetByIdAsync(bankBoxId);

        if (bankBox is null)
        {
            return null;
        }

        var hydratedBankBox = CloneItem(bankBox);
        await HydrateContainedItemsRecursiveAsync(hydratedBankBox);

        return hydratedBankBox;
    }

    public async Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
    {
        var character = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(characterId);

        if (character is null)
        {
            _logger.Warning("Character {CharacterId} not found", characterId);

            return null;
        }

        await HydrateCharacterEquipmentRuntimeAsync(character);

        _logger.Verbose("Loaded character {CharacterId}", characterId);

        return character;
    }

    public async Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
    {
        var account = await _persistenceService.UnitOfWork.Accounts.GetByIdAsync(accountId);

        if (account == null)
        {
            _logger.Warning("Cannot get characters for account {AccountId}: account not found", accountId);

            return [];
        }

        var characters = new List<UOMobileEntity>(account.CharacterIds.Count);

        foreach (var characterId in account.CharacterIds)
        {
            var mobile = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(characterId);

            if (mobile != null)
            {
                await HydrateCharacterEquipmentRuntimeAsync(mobile);
                characters.Add(mobile);
            }
        }

        _logger.Information(
            "Retrieved {CharacterCount} characters for account {AccountId}",
            characters.Count,
            accountId
        );

        return characters;
    }

    public async Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
    {
        var account = await _persistenceService.UnitOfWork.Accounts.GetByIdAsync(accountId);

        if (account == null)
        {
            _logger.Warning(
                "Cannot remove character {CharacterId} from account {AccountId}: account not found",
                characterId,
                accountId
            );

            return false;
        }

        var removed = account.CharacterIds.Remove(characterId);

        if (!removed)
        {
            _logger.Warning(
                "Cannot remove character {CharacterId} from account {AccountId}: character not linked",
                characterId,
                accountId
            );

            return false;
        }

        await _persistenceService.UnitOfWork.Accounts.UpsertAsync(account);
        _logger.Information("Removed character {CharacterId} from account {AccountId}", characterId, accountId);

        return true;
    }

    private async Task ApplyEquippedItemHueAsync(UOMobileEntity character, ItemLayerType layer, short hue)
    {
        if (!character.EquippedItemIds.TryGetValue(layer, out var itemId))
        {
            return;
        }

        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            return;
        }

        item.Hue = hue;
        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
    }

    private static UOItemEntity CloneItem(UOItemEntity item)
        => new()
        {
            Id = item.Id,
            Location = item.Location,
            Name = item.Name,
            Weight = item.Weight,
            Amount = item.Amount,
            IsStackable = item.IsStackable,
            Rarity = item.Rarity,
            ItemId = item.ItemId,
            Hue = item.Hue,
            GumpId = item.GumpId,
            ParentContainerId = item.ParentContainerId,
            ContainerPosition = item.ContainerPosition,
            EquippedMobileId = item.EquippedMobileId,
            EquippedLayer = item.EquippedLayer
        };

    private static StarterProfileContext CreateStarterProfileContext(UOMobileEntity character)
        => new(character.Profession, character.Race, character.Gender);

    private async Task EnsureStarterContainerItemAsync(UOMobileEntity character, UOItemEntity item)
    {
        var existing = await _persistenceService.UnitOfWork.Items.QueryAsync(
                           existingItem => existingItem.ParentContainerId == item.ParentContainerId &&
                                           existingItem.ItemId == item.ItemId,
                           static existingItem => existingItem
                       );

        if (existing.Count > 0)
        {
            return;
        }

        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        _logger.Debug("Created starter container item {ItemId:X4} for character {CharacterId}", item.ItemId, character.Id);
    }

    private async Task EnsureStarterEquippedItemAsync(
        UOMobileEntity character,
        ItemLayerType layer,
        StarterProfileContext starterProfileContext
    )
    {
        if (character.HasEquippedItem(layer))
        {
            return;
        }

        var item = _entityFactoryService.CreateStarterEquipment(character.Id, layer, starterProfileContext);

        character.AddEquippedItem(layer, item);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        _logger.Debug(
            "Created starter equipped item {ItemId:X4} on layer {Layer} for {CharacterId}",
            item.ItemId,
            layer,
            character.Id
        );
    }

    private async Task EnsureStarterInventoryAsync(UOMobileEntity character)
    {
        var starterProfileContext = CreateStarterProfileContext(character);
        UOItemEntity backpack;

        if (!character.HasEquippedItem(ItemLayerType.Backpack))
        {
            backpack = _entityFactoryService.CreateStarterBackpack(character.Id, starterProfileContext);
            character.AddEquippedItem(ItemLayerType.Backpack, backpack);
            character.BackpackId = backpack.Id;
        }
        else
        {
            character.BackpackId = character.EquippedItemIds[ItemLayerType.Backpack];
            backpack = await _persistenceService.UnitOfWork.Items.GetByIdAsync(character.BackpackId) ??
                       _entityFactoryService.CreateStarterBackpack(character.Id, starterProfileContext);
            backpack.Id = character.BackpackId;
        }

        await _persistenceService.UnitOfWork.Items.UpsertAsync(backpack);

        await EnsureStarterContainerItemAsync(
            character,
            _entityFactoryService.CreateStarterGold(backpack.Id, new(1, 1), StarterGoldQuantity, starterProfileContext)
        );
        await EnsureStarterEquippedItemAsync(character, ItemLayerType.Shirt, starterProfileContext);
        await EnsureStarterEquippedItemAsync(character, ItemLayerType.Pants, starterProfileContext);
        await EnsureStarterEquippedItemAsync(character, ItemLayerType.Shoes, starterProfileContext);
        await EnsureStarterEquippedItemAsync(character, ItemLayerType.Bank, starterProfileContext);
    }

    private async Task HydrateCharacterEquipmentRuntimeAsync(UOMobileEntity character)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (character.EquippedItemIds.Count == 0)
        {
            character.HydrateEquipmentRuntime([]);

            return;
        }

        var equippedItems = await _persistenceService.UnitOfWork.Items.QueryAsync(
                                item => item.EquippedMobileId == character.Id && item.EquippedLayer is not null,
                                static item => item
                            );

        var hydratedIds = new HashSet<Serial>(equippedItems.Count);

        foreach (var item in equippedItems)
        {
            hydratedIds.Add(item.Id);
        }

        // Collect IDs not found by the batch query
        var missingIds = new HashSet<Serial>();

        foreach (var (_, itemId) in character.EquippedItemIds)
        {
            if (!hydratedIds.Contains(itemId))
            {
                missingIds.Add(itemId);
            }
        }

        if (missingIds.Count == 0)
        {
            character.HydrateEquipmentRuntime(equippedItems);

            return;
        }

        // Single batch query for ALL missing items instead of N individual GetByIdAsync calls
        var missingItems = await _persistenceService.UnitOfWork.Items.QueryAsync(
                               item => missingIds.Contains(item.Id),
                               static item => item
                           );

        var inferredItems = new List<UOItemEntity>(missingItems.Count);

        foreach (var item in missingItems)
        {
            foreach (var (layer, itemId) in character.EquippedItemIds)
            {
                if (itemId != item.Id)
                {
                    continue;
                }

                item.EquippedMobileId = character.Id;
                item.EquippedLayer = layer;
                inferredItems.Add(item);

                break;
            }
        }

        if (inferredItems.Count > 0)
        {
            character.HydrateEquipmentRuntime([.. equippedItems, .. inferredItems]);

            return;
        }

        character.HydrateEquipmentRuntime(equippedItems);
    }

    private async Task HydrateContainedItemsRecursiveAsync(UOItemEntity container)
    {
        var containedItems = await _persistenceService.UnitOfWork.Items.QueryAsync(
                                 item => item.ParentContainerId == container.Id,
                                 static item => item
                             );

        foreach (var item in containedItems)
        {
            var cloned = CloneItem(item);
            container.AddItem(cloned, item.ContainerPosition);
            await HydrateContainedItemsRecursiveAsync(cloned);
        }
    }
}
