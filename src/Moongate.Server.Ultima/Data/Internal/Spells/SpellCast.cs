using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Spells;

namespace Moongate.Server.Ultima.Data.Internal.Spells;

/// <summary>
///     One cast in progress: the spell, the scroll it is read from, when it began, where it is and the timers it owns.
/// </summary>
internal sealed class SpellCast
{
    public SpellDefinition Spell { get; }

    public ItemEntity? Scroll { get; }

    public DateTimeOffset StartedAt { get; }

    public SpellCastPhaseType Phase { get; set; } = SpellCastPhaseType.Casting;

    public string? DelayTimer { get; set; }

    public string? GestureTimer { get; set; }

    public SpellCast(SpellDefinition spell, ItemEntity? scroll, DateTimeOffset startedAt)
    {
        Spell = spell;
        Scroll = scroll;
        StartedAt = startedAt;
    }
}
