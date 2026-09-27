using System.Collections.Frozen;
using Moongate.Core.Random;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Picks random names from the lists <see cref="Loaders.NamesLoader" /> loaded, the first time one is asked for.
/// </summary>
public class NameService : INameService
{
    private readonly Lazy<FrozenDictionary<string, List<string>>> _lists;

    public NameService(IDataLoaderService dataLoaderService)
    {
        _lists = new(
            () => dataLoaderService.GetEntities<NameList>()
                                   .ToFrozenDictionary(list => list.Id, list => list.Names, StringComparer.OrdinalIgnoreCase)
        );
    }

    public bool HasList(string listId)
    {
        return _lists.Value.ContainsKey(listId);
    }

    public string RandomName(string listId)
    {
        if (!_lists.Value.TryGetValue(listId, out var names))
        {
            throw new KeyNotFoundException($"No name list has id '{listId}'.");
        }

        return names[BuiltInRng.Next(names.Count)];
    }
}
