using Moongate.Server.Interfaces.Services.Magic;

namespace Moongate.Server.Services.Magic;

/// <summary>
/// Holds explicitly registered spells keyed by spell identifier.
/// </summary>
public sealed class SpellRegistry
{
    private readonly Dictionary<int, ISpell> _spells = [];

    public IReadOnlyDictionary<int, ISpell> All => _spells;

    public ISpell? Get(int spellId)
        => _spells.TryGetValue(spellId, out var spell) ? spell : null;

    public void Register(ISpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (!_spells.TryAdd(spell.SpellId, spell))
        {
            throw new InvalidOperationException($"Spell with id {spell.SpellId} is already registered.");
        }
    }
}
