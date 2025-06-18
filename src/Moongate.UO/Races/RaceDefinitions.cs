using Moongate.UO.Races.Base;

namespace Moongate.UO.Races;

public class RaceDefinitions
{
    public static void RegisterRace(Race race)
    {
        Race.Races[race.RaceIndex] = race;
        Race.AllRaces.Add(race);
    }
}
