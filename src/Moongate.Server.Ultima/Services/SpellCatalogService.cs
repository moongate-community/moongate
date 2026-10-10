using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The spells of <c>data/spells.toml</c>: see <see cref="ISpellCatalogService" />. Built on first use, after the
///     loaders and the item templates have run.
/// </summary>
public sealed class SpellCatalogService : ISpellCatalogService
{
    private readonly Lazy<IReadOnlyList<SpellDefinition>> _all;
    private readonly Lazy<FrozenDictionary<int, SpellDefinition>> _byId;
    private readonly Lazy<FrozenDictionary<string, SpellDefinition>> _byKey;
    private readonly Lazy<FrozenDictionary<int, SpellDefinition>> _byScroll;

    public IReadOnlyList<SpellDefinition> All => _all.Value;

    public SpellCatalogService(IDataLoaderService data, IItemTemplateService templates)
    {
        _all = new(() => data.GetEntities<SpellDefinition>().OrderBy(spell => spell.Id).ToList());
        _byId = new(() => _all.Value.ToFrozenDictionary(spell => spell.Id));
        _byKey = new(() => _all.Value.ToFrozenDictionary(spell => spell.Key, StringComparer.Ordinal));
        _byScroll = new(() =>
            {
                var scrolls = new Dictionary<int, SpellDefinition>();

                foreach (var spell in _all.Value)
                {
                    if (templates.TryGet(spell.Scroll, out var scroll))
                    {
                        scrolls[(int)scroll.ItemId.Value] = spell;
                    }
                }

                return scrolls.ToFrozenDictionary();
            }
        );
    }

    public bool TryGet(int id, [NotNullWhen(true)] out SpellDefinition? spell)
    {
        return _byId.Value.TryGetValue(id, out spell);
    }

    public bool TryGetByKey(string key, [NotNullWhen(true)] out SpellDefinition? spell)
    {
        return _byKey.Value.TryGetValue(key, out spell);
    }

    public bool TryGetByScrollGraphic(int graphic, [NotNullWhen(true)] out SpellDefinition? spell)
    {
        return _byScroll.Value.TryGetValue(graphic, out spell);
    }
}
