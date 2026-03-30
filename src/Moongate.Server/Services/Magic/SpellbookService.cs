using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic;

/// <summary>
/// Resolves available spellbooks for mobiles and persists spellbook bitfields on items.
/// </summary>
public sealed class SpellbookService : ISpellbookService
{
    private const string RegularSpellbookTemplateId = "spellbook";

    private readonly ICharacterService _characterService;

    public SpellbookService(ICharacterService characterService)
    {
        ArgumentNullException.ThrowIfNull(characterService);

        _characterService = characterService;
    }

    public SpellbookData GetData(UOItemEntity book)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (book.TryGetCustomInteger(ItemCustomParamKeys.Spellbook.Content, out var stored))
        {
            return new SpellbookData((ulong)stored);
        }

        return new SpellbookData(0UL);
    }

    public void SetData(UOItemEntity book, SpellbookData data)
    {
        ArgumentNullException.ThrowIfNull(book);

        book.SetCustomInteger(ItemCustomParamKeys.Spellbook.Content, unchecked((long)data.Content));
    }

    public async ValueTask<bool> MobileHasSpellAsync(
        UOMobileEntity mobile,
        SpellbookType spellbookType,
        int spellId,
        CancellationToken cancellationToken = default
    )
    {
        var book = await FindSpellbookAsync(mobile, spellbookType, cancellationToken);

        return book is not null && GetData(book).HasSpell(spellId);
    }

    public async ValueTask<UOItemEntity?> FindSpellbookAsync(
        UOMobileEntity mobile,
        SpellbookType spellbookType,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(mobile);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var item in mobile.GetEquippedItemsRuntime())
        {
            if (IsMatchingSpellbook(item, spellbookType))
            {
                return item;
            }
        }

        var backpack = await _characterService.GetBackpackWithItemsAsync(mobile);
        cancellationToken.ThrowIfCancellationRequested();

        return backpack is null ? null : FindSpellbookRecursive(backpack, spellbookType);
    }

    private static UOItemEntity? FindSpellbookRecursive(UOItemEntity container, SpellbookType spellbookType)
    {
        for (var index = 0; index < container.Items.Count; index++)
        {
            var item = container.Items[index];

            if (IsMatchingSpellbook(item, spellbookType))
            {
                return item;
            }

            var nested = FindSpellbookRecursive(item, spellbookType);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool IsMatchingSpellbook(UOItemEntity item, SpellbookType spellbookType)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) ||
            string.IsNullOrWhiteSpace(templateId))
        {
            return false;
        }

        var expectedTemplateId = GetTemplateId(spellbookType);

        return expectedTemplateId is not null &&
               string.Equals(templateId, expectedTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetTemplateId(SpellbookType spellbookType)
        => spellbookType switch
        {
            SpellbookType.Regular => RegularSpellbookTemplateId,
            _                     => null
        };
}
