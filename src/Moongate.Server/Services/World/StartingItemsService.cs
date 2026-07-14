using Moongate.Server.Interfaces.World;
using Moongate.UO.Data.StartingItems;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>In-memory starting-items table, populated at startup by <c>StartingItemsLoader</c>.</summary>
public sealed class StartingItemsService : IStartingItemsService
{
    private StartingItemsData _data = new();

    public void Load(StartingItemsData data)
    {
        _data = data;
    }

    public StartingItemKit Resolve(RaceType race, GenderType gender, IReadOnlyList<string> topSkillNames)
    {
        var kit = new StartingItemKit();

        Append(kit, _data.All);

        if (_data.ByBody.TryGetValue($"{race}/{gender}", out var body))
        {
            Append(kit, body);
        }

        foreach (var name in topSkillNames)
        {
            if (_data.BySkill.TryGetValue(name, out var skillKit))
            {
                Append(kit, skillKit);
            }
        }

        return kit;
    }

    private static void Append(StartingItemKit target, StartingItemKit source)
    {
        target.Equip.AddRange(source.Equip);
        target.Pack.AddRange(source.Pack);
    }
}
