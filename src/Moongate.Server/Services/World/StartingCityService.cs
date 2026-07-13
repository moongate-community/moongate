using Moongate.Server.Interfaces;
using Moongate.UO.Data.StartingCities;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory registry of character-creation starting cities, ordered by client index. Populated at
/// startup by <see cref="Moongate.Server.Loaders.StartingCitiesLoader" />. The list order is the index
/// the client receives in the char-list packet and echoes back on character creation.
/// </summary>
public sealed class StartingCityService : IStartingCityService
{
    private readonly List<StartingCity> _cities = [];

    public IReadOnlyList<StartingCity> All => _cities;

    public int Count => _cities.Count;

    public void Register(StartingCity city)
    {
        _cities.Add(city);
    }

    public StartingCity? GetByIndex(int index)
    {
        return index >= 0 && index < _cities.Count ? _cities[index] : null;
    }
}
