using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Magic;

/// <summary>
///     Knows the spells whose keys a test lists, answers their check with <see cref="Verdict" /> and records the casts.
/// </summary>
public sealed class StubSpellScriptService : ISpellScriptService
{
    public HashSet<string> Keys { get; } = [];

    /// <summary>
    ///     What <see cref="Check" /> answers: missing, which lets the cast go on, unless a test sets it.
    /// </summary>
    public ScriptResult Verdict { get; set; } = ScriptResult.Missing;

    public List<(string Key, MobileEntity Caster, SpellTargetInfo Target, bool FromScroll)> Checks { get; } = [];

    public List<(string Key, MobileEntity Caster, SpellTargetInfo Target, bool FromScroll)> Casts { get; } = [];

    public bool Has(SpellDefinition spell)
    {
        return Keys.Contains(spell.Key);
    }

    public ScriptResult Check(SpellDefinition spell, MobileEntity caster, SpellTargetInfo target, bool fromScroll)
    {
        Checks.Add((spell.Key, caster, target, fromScroll));

        return Verdict;
    }

    public ScriptResult Cast(SpellDefinition spell, MobileEntity caster, SpellTargetInfo target, bool fromScroll)
    {
        Casts.Add((spell.Key, caster, target, fromScroll));

        return ScriptResult.Completed([]);
    }
}
