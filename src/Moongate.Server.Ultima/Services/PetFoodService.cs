using System.Collections.Frozen;
using Moongate.Server.Ultima.Data.Pets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     What the pets eat: see <see cref="IPetFoodService" />. Built on first use, after the loaders have run.
/// </summary>
public sealed class PetFoodService : IPetFoodService
{
    private readonly ITamingService _taming;
    private readonly Lazy<FrozenDictionary<string, FrozenSet<string>>> _kinds;

    public PetFoodService(IDataLoaderService data, ITamingService taming)
    {
        _taming = taming;
        _kinds = new(() => data.GetEntities<PetFood>()
            .ToFrozenDictionary(food => food.Kind, food => food.Items.ToFrozenSet(StringComparer.Ordinal))
        );
    }

    public bool Accepts(string? creatureTemplate, string? itemTemplate)
    {
        if (creatureTemplate is null ||
            itemTemplate is null ||
            !_taming.TryGet(creatureTemplate, out var creature))
        {
            return false;
        }

        return creature.Food.Any(kind => _kinds.Value.TryGetValue(kind, out var items) && items.Contains(itemTemplate));
    }
}
