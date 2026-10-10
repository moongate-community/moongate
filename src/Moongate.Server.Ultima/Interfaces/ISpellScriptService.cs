using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Calls the script of a spell, the global table named after its key, defined by
///     <c>scripts/spells/&lt;key&gt;.lua</c>: <c>magic_arrow</c>, <c>heal</c>. Called on the game loop; nothing runs before
///     the scripts are loaded or once they stop.
/// </summary>
public interface ISpellScriptService
{
    /// <summary>
    ///     Gets whether <c>scripts/spells/&lt;key&gt;.lua</c> exists: a spell without one cannot be cast.
    /// </summary>
    bool Has(SpellDefinition spell);

    /// <summary>
    ///     Calls <c>check(caster, target, spell)</c> of the spell's script, which may refuse the cast before anything is
    ///     spent by returning a cliloc number or a text; <see cref="ScriptResult.Missing" /> when it has none.
    /// </summary>
    ScriptResult Check(SpellDefinition spell, MobileEntity caster, SpellTargetInfo target, bool fromScroll);

    /// <summary>
    ///     Calls <c>cast(caster, target, spell)</c> of the spell's script, once the cast succeeded;
    ///     <see cref="ScriptResult.Missing" /> when the spell has no script or no such function.
    /// </summary>
    ScriptResult Cast(SpellDefinition spell, MobileEntity caster, SpellTargetInfo target, bool fromScroll);
}
