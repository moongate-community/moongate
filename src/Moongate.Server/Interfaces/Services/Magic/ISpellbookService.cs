using Moongate.Server.Data.Magic;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Magic;

/// <summary>
/// Reads spellbook content and resolves available spellbooks for a mobile.
/// </summary>
public interface ISpellbookService
{
    /// <summary>
    /// Reads the spellbook bitfield stored on an item.
    /// </summary>
    /// <param name="book">Spellbook item entity.</param>
    /// <returns>Decoded spellbook data; empty when the property is not present.</returns>
    SpellbookData GetData(UOItemEntity book);

    /// <summary>
    /// Writes spellbook bitfield content onto an item.
    /// </summary>
    /// <param name="book">Spellbook item entity.</param>
    /// <param name="data">Spellbook content to persist.</param>
    void SetData(UOItemEntity book, SpellbookData data);

    /// <summary>
    /// Returns whether the mobile has an available spellbook of the requested family that contains the spell.
    /// </summary>
    /// <param name="mobile">Mobile being checked.</param>
    /// <param name="spellbookType">Required spellbook family.</param>
    /// <param name="spellId">Spell identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> MobileHasSpellAsync(
        UOMobileEntity mobile,
        SpellbookType spellbookType,
        int spellId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Finds the first available spellbook of the requested family for a mobile.
    /// </summary>
    /// <param name="mobile">Mobile being checked.</param>
    /// <param name="spellbookType">Required spellbook family.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching spellbook when found; otherwise <see langword="null"/>.</returns>
    ValueTask<UOItemEntity?> FindSpellbookAsync(
        UOMobileEntity mobile,
        SpellbookType spellbookType,
        CancellationToken cancellationToken = default
    );
}
