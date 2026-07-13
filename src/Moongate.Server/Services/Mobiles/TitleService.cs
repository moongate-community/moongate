using Moongate.Server.Interfaces.Mobiles;
using Moongate.UO.Data.Titles;

namespace Moongate.Server.Services.Mobiles;

/// <summary>
/// Fame/karma noble-title lookup table. Populated at startup by
/// <see cref="Moongate.Server.Loaders.TitlesLoader" />. Replicates the classic UO title resolution:
/// fame selects a tier, karma selects a row within it.
/// </summary>
public sealed class TitleService : ITitleService
{
    private readonly List<FameTitleGroup> _groups = [];

    public IReadOnlyList<FameTitleGroup> All => _groups;

    public int Count => _groups.Count;

    public void Register(FameTitleGroup group)
    {
        _groups.Add(group);
    }

    public string GetTitle(string name, int fame, int karma, bool female)
    {
        for (var i = 0; i < _groups.Count; i++)
        {
            var group = _groups[i];

            if (fame > group.Fame && i != _groups.Count - 1)
            {
                continue;
            }

            var karmaEntries = group.Karma;

            for (var j = 0; j < karmaEntries.Count; j++)
            {
                var entry = karmaEntries[j];

                if (karma <= entry.Karma || j == karmaEntries.Count - 1)
                {
                    return string.Format(entry.Title, name, female ? "Lady" : "Lord");
                }
            }

            break;
        }

        return name;
    }
}
