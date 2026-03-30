using Moongate.Server.Data.Magic;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Magic;

/// <summary>
/// Represents a castable spell definition and effect entrypoint.
/// </summary>
public interface ISpell
{
    /// <summary>
    /// Gets the unique numeric identifier used to register the spell.
    /// </summary>
    int SpellId { get; }

    /// <summary>
    /// Gets the spellbook family required to cast the spell.
    /// </summary>
    SpellbookType SpellbookType { get; }

    /// <summary>
    /// Gets static spell metadata such as display name, mantra, and reagent requirements.
    /// </summary>
    SpellInfo Info { get; }

    /// <summary>
    /// Gets the base mana cost before mobile modifiers are applied.
    /// </summary>
    int ManaCost { get; }

    /// <summary>
    /// Gets the base cast delay before mobile modifiers are applied.
    /// </summary>
    TimeSpan CastDelay { get; }

    /// <summary>
    /// Gets how the spell expects target resolution to behave after cast completion.
    /// </summary>
    SpellTargetingType Targeting { get; }

    /// <summary>
    /// Gets the minimum skill required to attempt the spell.
    /// </summary>
    double MinSkill { get; }

    /// <summary>
    /// Gets the skill value above which the spell is treated as fully reliable.
    /// </summary>
    double MaxSkill { get; }

    /// <summary>
    /// Applies the resolved spell effect after the cast sequence completes.
    /// </summary>
    /// <param name="caster">Mobile that cast the spell.</param>
    /// <param name="target">Optional mobile target.</param>
    void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target);
}
