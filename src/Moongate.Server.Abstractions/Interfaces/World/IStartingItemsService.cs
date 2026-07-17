using Moongate.UO.Data.StartingItems;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>Holds the starting-items table and resolves the kit for a new character.</summary>
public interface IStartingItemsService
{
    /// <summary>Replaces the loaded table.</summary>
    void Load(StartingItemsData data);

    /// <summary>Merges the universal kit, the body kit and the top skills' kits into one.</summary>
    StartingItemKit Resolve(RaceType race, GenderType gender, IReadOnlyList<string> topSkillNames);
}
