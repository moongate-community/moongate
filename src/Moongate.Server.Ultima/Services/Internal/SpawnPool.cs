using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     What a spawn region picks its NPCs from, as UOX3's <c>NPC</c> and <c>NPCLIST</c> lines: each mobile weighs 1,
///     each
///     entry of a list its own weight, and an entry naming another list picks again from that list.
/// </summary>
internal sealed class SpawnPool
{
    private readonly List<NpcListEntry> _entries;
    private readonly IReadOnlyDictionary<string, NpcListTemplate> _lists;

    public SpawnPool(SpawnTemplate spawn, IReadOnlyDictionary<string, NpcListTemplate> lists)
    {
        _lists = lists;
        _entries = spawn.MobileIds
            .Select(id => new NpcListEntry { MobileId = id })
            .Concat(spawn.NpcListIds.SelectMany(id => lists[id].Entries))
            .ToList();
    }

    public string Pick(Random random)
    {
        var entry = Roll(_entries, random);

        // The loaders refuse list loops, so this ends.
        while (entry.NpcListId is { } list)
        {
            entry = Roll(_lists[list].Entries, random);
        }

        // Safe: list entries always carry a mobile id.
        return entry.MobileId!;
    }

    private static NpcListEntry Roll(List<NpcListEntry> entries, Random random)
    {
        var roll = random.Next(0, entries.Sum(entry => entry.Weight));

        foreach (var entry in entries)
        {
            roll -= entry.Weight;

            if (roll < 0)
            {
                return entry;
            }
        }

        return entries[^1];
    }
}
