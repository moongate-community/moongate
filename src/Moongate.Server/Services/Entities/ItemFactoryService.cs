using System.Globalization;
using Moongate.Core.Extensions.Strings;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.World;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Creates item entities from template definitions and fallback defaults.
/// </summary>
public sealed class ItemFactoryService : IItemFactoryService
{
    private const int BackpackItemId = 0x0E75;
    private const int DefaultWritableBookPages = 20;
    private const string FlippableItemIdsKey = "flippable_item_ids";

    private readonly ILogger _logger = Log.ForContext<ItemFactoryService>();
    private readonly IItemTemplateService _itemTemplateService;
    private readonly IPersistenceService _persistenceService;
    private readonly IBookTemplateService? _bookTemplateService;

    public ItemFactoryService(
        IItemTemplateService itemTemplateService,
        IPersistenceService persistenceService,
        IBookTemplateService? bookTemplateService = null
    )
    {
        _itemTemplateService = itemTemplateService;
        _persistenceService = persistenceService;
        _bookTemplateService = bookTemplateService;
    }

    /// <inheritdoc />
    public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemTemplateId);

        if (!TryGetItemTemplate(itemTemplateId, out var template) || template is null)
        {
            throw new InvalidOperationException($"Item template '{itemTemplateId}' not found.");
        }

        var item = new UOItemEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            Name = template.Name,
            Weight = (int)template.Weight,
            Amount = 1,
            Rarity = template.Rarity,
            Visibility = template.Visibility,
            ItemId = ParseItemId(template.ItemId),
            Hue = template.Hue.Resolve(),
            GumpId = ResolveGumpId(template),
            ScriptId = template.ScriptId,
            Location = Point3D.Zero,
            ParentContainerId = Serial.Zero,
            ContainerPosition = Point2D.Zero,
            EquippedMobileId = Serial.Zero,
            EquippedLayer = null
        };

        var itemFromTile = TileData.ItemTable[item.ItemId];

        if (string.IsNullOrEmpty(item.Name))
        {
            item.Name = itemFromTile.Name;
        }

        item.IsStackable = itemFromTile[UOTileFlag.Generic];

        if (item.Weight == 0)
        {
            item.Weight = itemFromTile.Weight;
        }

        ApplyTemplateParams(item, template);
        ApplyBookTemplate(item, template);
        EnsureWritableBookMetadata(item);
        item.CombatStats = CreateCombatStats(template);
        item.Modifiers = CreateModifiers(template);

        return item;
    }

    /// <inheritdoc />
    public UOItemEntity GetNewBackpack()
    {
        if (_itemTemplateService.TryGet("backpack", out _))
        {
            return CreateItemFromTemplate("backpack");
        }

        _logger.Warning("Backpack template not found. Using hardcoded fallback backpack item.");

        return new()
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            Name = "Backpack",
            Weight = 0,
            Amount = 1,
            IsStackable = false,
            Rarity = ItemRarity.Common,
            Visibility = AccountType.Regular,
            ItemId = BackpackItemId,
            Hue = 0,
            ScriptId = "none",
            Location = Point3D.Zero,
            ParentContainerId = Serial.Zero,
            ContainerPosition = Point2D.Zero,
            EquippedMobileId = Serial.Zero,
            EquippedLayer = null
        };
    }

    /// <inheritdoc />
    public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(itemTemplateId))
        {
            return false;
        }

        var normalizedTemplateId = itemTemplateId.Trim();

        if (_itemTemplateService.TryGet(normalizedTemplateId, out definition))
        {
            return true;
        }

        var snakeCaseTemplateId = normalizedTemplateId.ToSnakeCase();

        if (!string.Equals(snakeCaseTemplateId, normalizedTemplateId, StringComparison.Ordinal) &&
            _itemTemplateService.TryGet(snakeCaseTemplateId, out definition))
        {
            return true;
        }

        return false;
    }

    private void ApplyBookTemplate(UOItemEntity item, ItemTemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(template.BookId))
        {
            return;
        }

        if (_bookTemplateService is null)
        {
            throw new InvalidOperationException(
                $"Item template '{template.Id}' references book '{template.BookId}' but no book template service is configured."
            );
        }

        if (!_bookTemplateService.TryLoad(template.BookId, null, out var book) || book is null)
        {
            throw new InvalidOperationException(
                $"Item template '{template.Id}' references missing or invalid book template '{template.BookId}'."
            );
        }

        item.SetCustomString(ItemCustomParamKeys.Book.BookId, template.BookId.Trim());
        item.SetCustomString(ItemCustomParamKeys.Book.Title, book.Title);
        item.SetCustomString(ItemCustomParamKeys.Book.Author, book.Author);
        item.SetCustomString(ItemCustomParamKeys.Book.Content, book.Content);

        if (book.ReadOnly.HasValue)
        {
            item.SetCustomBoolean(ItemCustomParamKeys.Book.Writable, !book.ReadOnly.Value);
        }
    }

    private static void ApplyDoorFacingOverride(UOItemEntity item, ItemTemplateDefinition template)
    {
        if (!IsDoorTemplate(template) ||
            !template.Params.TryGetValue("Facing", out var facingParam) ||
            !Enum.TryParse<DoorGenerationFacing>(facingParam.Value, true, out var facing))
        {
            return;
        }

        item.Direction = facing.ToDirectionType();
        item.ItemId = facing.ToItemId(item.ItemId);
        item.SetCustomString(ItemCustomParamKeys.Door.Facing, facing.ToString());
    }

    private static void ApplyTemplateParams(UOItemEntity item, ItemTemplateDefinition template)
    {
        if (template.Dyeable)
        {
            item.SetCustomBoolean(ItemCustomParamKeys.Item.Dyeable, true);
        }

        if (template.FlippableItemIds.Count > 0)
        {
            item.SetCustomString(FlippableItemIdsKey, string.Join(',', template.FlippableItemIds));
        }

        foreach (var (key, param) in template.Params)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    $"Item template '{template.Id}' has an invalid params entry with an empty key."
                );
            }

            var normalizedKey = key.Trim();

            if (string.Equals(normalizedKey, "writable", StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(param.Value, out var writable))
            {
                item.SetCustomBoolean(ItemCustomParamKeys.Book.Writable, writable);

                continue;
            }

            if (string.Equals(normalizedKey, "pages", StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(param.Value, CultureInfo.InvariantCulture, out var pages))
            {
                item.SetCustomInteger(ItemCustomParamKeys.Book.Pages, pages);

                continue;
            }

            switch (param.Type)
            {
                case ItemTemplateParamType.String:
                    item.SetCustomString(normalizedKey, param.Value);

                    break;
                case ItemTemplateParamType.Serial:
                    if (!Serial.TryParse(param.Value, null, out var serial))
                    {
                        throw new InvalidOperationException(
                            $"Item template '{template.Id}' has invalid serial param '{normalizedKey}' = '{param.Value}'."
                        );
                    }

                    item.SetCustomInteger(normalizedKey, serial.Value);

                    break;
                case ItemTemplateParamType.Hue:
                    try
                    {
                        var resolvedHue = HueSpec.ParseFromString(param.Value).Resolve();
                        item.SetCustomInteger(normalizedKey, resolvedHue);
                    }
                    catch (FormatException exception)
                    {
                        throw new InvalidOperationException(
                            $"Item template '{template.Id}' has invalid hue param '{normalizedKey}' = '{param.Value}'.",
                            exception
                        );
                    }

                    break;
                default:
                    throw new InvalidOperationException(
                        $"Item template '{template.Id}' has unsupported param type '{param.Type}' for key '{normalizedKey}'."
                    );
            }
        }

        ApplyDoorFacingOverride(item, template);
    }

    private static ItemCombatStats? CreateCombatStats(ItemTemplateDefinition template)
    {
        if (template.Strength == 0 &&
            template.Dexterity == 0 &&
            template.Intelligence == 0 &&
            template.LowDamage == 0 &&
            template.HighDamage == 0 &&
            template.Defense == 0 &&
            template.Speed == 0 &&
            template.BaseRange == 0 &&
            template.MaxRange == 0 &&
            template.HitPoints == 0)
        {
            return null;
        }

        return new()
        {
            MinStrength = template.Strength,
            MinDexterity = template.Dexterity,
            MinIntelligence = template.Intelligence,
            DamageMin = template.LowDamage,
            DamageMax = template.HighDamage,
            Defense = template.Defense,
            AttackSpeed = template.Speed,
            RangeMin = template.BaseRange,
            RangeMax = template.MaxRange,
            MaxDurability = template.HitPoints,
            CurrentDurability = template.HitPoints
        };
    }

    private static ItemModifiers? CreateModifiers(ItemTemplateDefinition template)
    {
        if (template.StrengthAdd == 0 &&
            template.DexterityAdd == 0 &&
            template.IntelligenceAdd == 0 &&
            template.PhysicalResist == 0 &&
            template.FireResist == 0 &&
            template.ColdResist == 0 &&
            template.PoisonResist == 0 &&
            template.EnergyResist == 0 &&
            template.HitChanceIncrease == 0 &&
            template.DefenseChanceIncrease == 0 &&
            template.DamageIncrease == 0 &&
            template.SwingSpeedIncrease == 0 &&
            template.SpellDamageIncrease == 0 &&
            template.FasterCasting == 0 &&
            template.FasterCastRecovery == 0 &&
            template.LowerManaCost == 0 &&
            template.LowerReagentCost == 0 &&
            template.Luck == 0 &&
            !template.SpellChanneling &&
            template.UsesRemaining == 0)
        {
            return null;
        }

        return new()
        {
            StrengthBonus = template.StrengthAdd,
            DexterityBonus = template.DexterityAdd,
            IntelligenceBonus = template.IntelligenceAdd,
            PhysicalResist = template.PhysicalResist,
            FireResist = template.FireResist,
            ColdResist = template.ColdResist,
            PoisonResist = template.PoisonResist,
            EnergyResist = template.EnergyResist,
            HitChanceIncrease = template.HitChanceIncrease,
            DefenseChanceIncrease = template.DefenseChanceIncrease,
            DamageIncrease = template.DamageIncrease,
            SwingSpeedIncrease = template.SwingSpeedIncrease,
            SpellDamageIncrease = template.SpellDamageIncrease,
            FasterCasting = template.FasterCasting,
            FasterCastRecovery = template.FasterCastRecovery,
            LowerManaCost = template.LowerManaCost,
            LowerReagentCost = template.LowerReagentCost,
            Luck = template.Luck,
            SpellChanneling = template.SpellChanneling ? 1 : 0,
            UsesRemaining = template.UsesRemaining
        };
    }

    private static void EnsureWritableBookMetadata(UOItemEntity item)
    {
        if (!item.TryGetCustomBoolean(ItemCustomParamKeys.Book.Writable, out var writable) || !writable)
        {
            return;
        }

        if (!item.TryGetCustomString(ItemCustomParamKeys.Book.Title, out _))
        {
            item.SetCustomString(ItemCustomParamKeys.Book.Title, string.Empty);
        }

        if (!item.TryGetCustomString(ItemCustomParamKeys.Book.Author, out _))
        {
            item.SetCustomString(ItemCustomParamKeys.Book.Author, string.Empty);
        }

        if (!item.TryGetCustomString(ItemCustomParamKeys.Book.Content, out _))
        {
            item.SetCustomString(ItemCustomParamKeys.Book.Content, string.Empty);
        }

        if (!item.TryGetCustomInteger(ItemCustomParamKeys.Book.Pages, out _))
        {
            item.SetCustomInteger(ItemCustomParamKeys.Book.Pages, DefaultWritableBookPages);
        }
    }

    private static bool IsDoorTemplate(ItemTemplateDefinition template)
        => string.Equals(template.ScriptId, "items.door", StringComparison.OrdinalIgnoreCase) ||
           template.Tags.Any(
               static tag =>
                   string.Equals(tag, "door", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tag, "gate", StringComparison.OrdinalIgnoreCase)
           );

    private static int ParseItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(trimmed, CultureInfo.InvariantCulture);
    }

    private static int? ParseOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(trimmed, CultureInfo.InvariantCulture);
    }

    private static int? ResolveGumpId(ItemTemplateDefinition template)
    {
        var templateGumpId = ParseOptionalInt(template.GumpId);

        if (templateGumpId.HasValue)
        {
            return templateGumpId;
        }

        var itemId = ParseItemId(template.ItemId);

        if (ContainerLayoutSystem.ContainerBagDefsByItemId.TryGetValue(itemId, out var byItemId))
        {
            return byItemId.GumpId;
        }

        if (!string.IsNullOrWhiteSpace(template.ContainerLayoutId) &&
            ContainerLayoutSystem.ContainerBagDefsById.TryGetValue(template.ContainerLayoutId.Trim(), out var byLayoutId))
        {
            return byLayoutId.GumpId;
        }

        return null;
    }
}
