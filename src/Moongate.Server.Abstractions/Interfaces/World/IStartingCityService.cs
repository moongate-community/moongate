using Moongate.UO.Data.StartingCities;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>In-memory registry of character-creation starting cities, ordered by client index.</summary>
public interface IStartingCityService
{
    /// <summary>All registered starting cities in load order (the order is the client index).</summary>
    IReadOnlyList<StartingCity> All { get; }

    /// <summary>Number of registered starting cities.</summary>
    int Count { get; }

    /// <summary>Returns the starting city at the given client index, or null when out of range.</summary>
    StartingCity? GetByIndex(int index);

    /// <summary>Adds a starting city to the registry, preserving order.</summary>
    void Register(StartingCity city);
}
