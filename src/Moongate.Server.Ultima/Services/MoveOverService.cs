using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the <c>on_move_over</c> of the scripted items a mobile steps on, through <see cref="IItemScriptService" />.
/// </summary>
public sealed class MoveOverService : IMoveOverService
{
    public const string MoveOverFunction = "on_move_over";

    // ModernUO's Mobile.Move: an item counts while the mobile's feet plus this are above it.
    private const int MobileReach = 15;

    private readonly ISectorService _sectors;
    private readonly IItemScriptService _scripts;
    private readonly ITileDataService _tiles;

    public MoveOverService(ISectorService sectors, IItemScriptService scripts, ITileDataService tiles)
    {
        _sectors = sectors;
        _scripts = scripts;
        _tiles = tiles;
    }

    public void SteppedOn(MobileEntity mobile)
    {
        var map = mobile.Map;
        var cell = mobile.Location;

        // The list is the service's own copy: a script may move or delete items.
        foreach (var item in _sectors.GetItemsInRange(map, cell, 0))
        {
            if (item.GroundLocation is not { } spot || !Touches(item, spot.Z, cell.Z) || !_scripts.HasScript(item))
            {
                continue;
            }

            _scripts.Run(item, MoveOverFunction, (long)mobile.Id.Value);

            if (mobile.Map != map || mobile.Location != cell)
            {
                return;
            }
        }
    }

    private bool Touches(ItemEntity item, int itemZ, int mobileZ)
    {
        if (itemZ == mobileZ)
        {
            return true;
        }

        var height = _tiles.TryGetItem(item.ItemId, out var tile) ? tile.Height : 0;

        return itemZ + height > mobileZ && mobileZ + MobileReach > itemZ;
    }
}
