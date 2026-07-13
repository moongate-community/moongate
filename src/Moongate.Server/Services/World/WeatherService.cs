using Moongate.Server.Interfaces.World;
using Moongate.UO.Data.Weather;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory registry of weather profiles, queryable by id. Populated at startup by
/// <see cref="Moongate.Server.Loaders.WeatherLoader" />.
/// </summary>
public sealed class WeatherService : IWeatherService
{
    private readonly Dictionary<int, WeatherType> _byId = new();

    public IReadOnlyList<WeatherType> All => [.. _byId.Values.OrderBy(weather => weather.Id)];

    public int Count => _byId.Count;

    public void Register(WeatherType weather)
    {
        _byId[weather.Id] = weather;
    }

    public WeatherType? GetById(int id)
    {
        return _byId.GetValueOrDefault(id);
    }
}
