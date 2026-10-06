using Moongate.Server.Ultima.Data.Titles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Titles;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services.Titles;

/// <summary>
///     Selects a fame band, then a karma band, from the title grid loaded at startup.
/// </summary>
public sealed class FameKarmaTitleService : IFameKarmaTitleService
{
    private readonly Lazy<FameKarmaTitle[]> _rows;

    public FameKarmaTitleService(IDataLoaderService dataLoaderService)
    {
        _rows = new(() => dataLoaderService.GetEntities<FameKarmaTitle>()
            .OrderBy(row => row.Fame)
            .ThenBy(row => row.Karma)
            .ToArray()
        );
    }

    public string GetTitle(int fame, int karma, GenderType gender)
    {
        var rows = _rows.Value;
        var selectedFame = rows[0].Fame;

        foreach (var row in rows)
        {
            if (row.Fame > fame)
            {
                break;
            }

            selectedFame = row.Fame;
        }

        FameKarmaTitle? selected = null;

        foreach (var row in rows)
        {
            if (row.Fame != selectedFame)
            {
                continue;
            }

            selected ??= row;

            if (row.Karma > karma)
            {
                break;
            }

            selected = row;
        }

        return gender == GenderType.Female && selected!.FemaleTitle is not null
            ? selected.FemaleTitle
            : selected.Title;
    }

    public string GetTitle(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        return GetTitle(mobile.Fame, mobile.Karma, mobile.Gender);
    }
}
