using Moongate.UO.Data.Weather;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>In-memory registry of weather profiles, queryable by id.</summary>
public interface IWeatherService
{
    /// <summary>All registered weather types, ordered by id.</summary>
    IReadOnlyList<WeatherType> All { get; }

    /// <summary>Number of registered weather types.</summary>
    int Count { get; }

    /// <summary>Returns the weather type with the given id, or null.</summary>
    WeatherType? GetById(int id);

    /// <summary>Adds or replaces a weather type, indexed by id.</summary>
    void Register(WeatherType weather);
}
