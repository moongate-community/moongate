using Moongate.Server.Ultima.Types.Spells;

namespace Moongate.Server.Ultima.Data.Spells;

/// <summary>
///     One <c>[[spell]]</c> of <c>data/spells.toml</c>: what a spell is, as data. What it does is the script
///     <c>scripts/spells/&lt;key&gt;.lua</c>; its mana and its skill window come from its circle.
/// </summary>
public class SpellDefinition
{
    /// <summary>
    ///     The client's number of the spell, 1 to 64: its bit in the spellbook and what a cast request names.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The name of the spell's script and of the spell in scripts, a lower-case identifier such as
    ///     <c>magic_arrow</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    ///     The name players read.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The circle, 1 to 8.
    /// </summary>
    public int Circle { get; set; }

    /// <summary>
    ///     The words of power said over the caster's head.
    /// </summary>
    public string Mantra { get; set; } = string.Empty;

    /// <summary>
    ///     The animation of a human caster: 16 or 17.
    /// </summary>
    public int Action { get; set; }

    /// <summary>
    ///     The reagents a cast takes from the backpack.
    /// </summary>
    public List<SpellReagent> Reagents { get; set; } = [];

    /// <summary>
    ///     What the target cursor asks for.
    /// </summary>
    public SpellTargetType Target { get; set; }

    /// <summary>
    ///     Whether the spell hurts or curses: its cursor is the harmful one.
    /// </summary>
    public bool Harmful { get; set; }

    /// <summary>
    ///     Whether Resisting Spells may weaken it.
    /// </summary>
    public bool Resistable { get; set; }

    /// <summary>
    ///     Whether Magic Reflection may turn it back.
    /// </summary>
    public bool Reflectable { get; set; }

    /// <summary>
    ///     The sound of the spell; 0 for none.
    /// </summary>
    public int Sound { get; set; }

    /// <summary>
    ///     The graphic played on the target; 0 for none.
    /// </summary>
    public int Effect { get; set; }

    /// <summary>
    ///     How long the <see cref="Effect" /> lasts.
    /// </summary>
    public int EffectDuration { get; set; }

    /// <summary>
    ///     The graphic that flies from the caster to the target; 0 for none.
    /// </summary>
    public int Projectile { get; set; }

    /// <summary>
    ///     How fast the <see cref="Projectile" /> flies.
    /// </summary>
    public int ProjectileSpeed { get; set; }

    /// <summary>
    ///     The text of the target cursor; empty for a spell with no target.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    ///     The id of the item template of the scroll that holds the spell.
    /// </summary>
    public string Scroll { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the spell is in the game.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets the bit of the spell in a spellbook's 64-bit mask.
    /// </summary>
    public ulong Bit => 1UL << (Id - 1);
}
