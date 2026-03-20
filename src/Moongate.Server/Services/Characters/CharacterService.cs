using System.Text.Json;
using Moongate.Server.Data.Entities;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Startup;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Geometry;
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
    private readonly ILogger _logger = Log.ForContext<CharacterService>();
    private readonly IPersistenceService _persistenceService;
    private readonly IEntityFactoryService _entityFactoryService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IStartupLoadoutScriptService _startupLoadoutScriptService;

    public CharacterService(
        IPersistenceService persistenceService,
        IEntityFactoryService entityFactoryService,
        IGameEventBusService gameEventBusService,
        IStartupLoadoutScriptService startupLoadoutScriptService
    )
    {
        _persistenceService = persistenceService;
        _entityFactoryService = entityFactoryService;
        _gameEventBusService = gameEventBusService;
        _startupLoadoutScriptService = startupLoadoutScriptService;
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

    private static void ApplyItemArgument(UOItemEntity item, string key, JsonElement value)
    {
        var normalizedKey = key.Trim();

        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                item.SetCustomString(MapItemArgumentKey(normalizedKey), value.GetString());

                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integerValue))
                {
                    item.SetCustomInteger(MapItemArgumentKey(normalizedKey), integerValue);

                    break;
                }

                if (value.TryGetDouble(out var doubleValue))
                {
                    item.SetCustomDouble(MapItemArgumentKey(normalizedKey), doubleValue);
                }

                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                item.SetCustomBoolean(MapItemArgumentKey(normalizedKey), value.GetBoolean());

                break;
            case JsonValueKind.Null:
                item.RemoveCustomProperty(MapItemArgumentKey(normalizedKey));

                break;
        }
    }

    private static void ApplyItemArguments(UOItemEntity item, JsonElement? args)
    {
        if (args is not { ValueKind: JsonValueKind.Object } typedArgs)
        {
            return;
        }

        foreach (var property in typedArgs.EnumerateObject())
        {
            ApplyItemArgument(item, property.Name, property.Value);
        }
    }

    private async Task EnsureStarterBackpackAsync(UOMobileEntity character)
    {
        if (character.HasEquippedItem(ItemLayerType.Backpack))
        {
            character.BackpackId = character.EquippedItemIds[ItemLayerType.Backpack];

            return;
        }

        var backpack = _entityFactoryService.GetNewBackpack();
        backpack.EquippedMobileId = character.Id;
        backpack.EquippedLayer = ItemLayerType.Backpack;
        backpack.ParentContainerId = Serial.Zero;
        backpack.ContainerPosition = Point2D.Zero;
        backpack.Location = Point3D.Zero;
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        character.BackpackId = backpack.Id;
        await _persistenceService.UnitOfWork.Items.UpsertAsync(backpack);
    }

    private async Task PersistBackpackItemAsync(
        UOMobileEntity character,
        UOItemEntity backpack,
        StartupLoadoutItem itemDefinition,
        int index
    )
    {
        var item = _entityFactoryService.CreateItemFromTemplate(itemDefinition.TemplateId);
        item.Amount = Math.Max(1, itemDefinition.Amount);
        ApplyItemArguments(item, itemDefinition.Args);

        var position = new Moongate.UO.Data.Geometry.Point2D(index + 1, index + 1);
        backpack.AddItem(item, position);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        _logger.Debug(
            "Created starter backpack item {TemplateId} for character {CharacterId}",
            itemDefinition.TemplateId,
            character.Id
        );
    }

    private async Task PersistEquippedItemAsync(UOMobileEntity character, StartupLoadoutItem itemDefinition)
    {
        if (itemDefinition.Layer is null)
        {
            throw new InvalidOperationException(
                $"Startup equip item '{itemDefinition.TemplateId}' is missing required layer."
            );
        }

        if (character.HasEquippedItem(itemDefinition.Layer.Value))
        {
            return;
        }

        var item = _entityFactoryService.CreateItemFromTemplate(itemDefinition.TemplateId);
        item.Amount = Math.Max(1, itemDefinition.Amount);
        item.ParentContainerId = Serial.Zero;
        item.ContainerPosition = Point2D.Zero;
        item.EquippedMobileId = character.Id;
        item.EquippedLayer = itemDefinition.Layer.Value;

        if (itemDefinition.Layer == ItemLayerType.Bank && item.GumpId is null)
        {
            item.GumpId = 0x0042;
        }

        ApplyItemArguments(item, itemDefinition.Args);
        character.AddEquippedItem(itemDefinition.Layer.Value, item);

        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        _logger.Debug(
            "Created starter equipped item {TemplateId} on layer {Layer} for {CharacterId}",
            itemDefinition.TemplateId,
            itemDefinition.Layer.Value,
            character.Id
        );
    }

    private async Task EnsureStarterInventoryAsync(UOMobileEntity character)
    {
        var starterProfileContext = CreateStarterProfileContext(character);
        await EnsureStarterBackpackAsync(character);
        var backpack = await _persistenceService.UnitOfWork.Items.GetByIdAsync(character.BackpackId) ??
                       throw new InvalidOperationException(
                           $"Starter backpack '{character.BackpackId}' was not created for character '{character.Id}'."
                       );
        var loadout = _startupLoadoutScriptService.BuildLoadout(
            starterProfileContext,
            character.Name ?? string.Empty
        );
        await _persistenceService.UnitOfWork.Items.UpsertAsync(backpack);

        for (var i = 0; i < loadout.Backpack.Count; i++)
        {
            await PersistBackpackItemAsync(character, backpack, loadout.Backpack[i], i);
        }

        foreach (var equippedItem in loadout.Equip)
        {
            await PersistEquippedItemAsync(character, equippedItem);
        }
    }

    private static string MapItemArgumentKey(string key)
        => key.ToLowerInvariant() switch
        {
            "title" => ItemCustomParamKeys.Book.Title,
            "author" => ItemCustomParamKeys.Book.Author,
            "content" => ItemCustomParamKeys.Book.Content,
            "pages" => ItemCustomParamKeys.Book.Pages,
            "writable" => ItemCustomParamKeys.Book.Writable,
            _ => key
        };

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
