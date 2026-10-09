using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Tells which NPCs open doors and asks the door in front of one to open, through the door's own script.
/// </summary>
public sealed class NpcDoorService : INpcDoorService
{
    private const string OpenFunction = "on_npc_use";

    // A door stands in the way of a mover within the mover's height of it, as ModernUO's AI counts it.
    private const int MoverHeight = 16;

    private readonly ISectorService _sectors;
    private readonly ITileDataService _tiles;
    private readonly IItemScriptService _scripts;
    private readonly IMobileTemplateService _templates;
    private readonly Lazy<Dictionary<int, BodyType>> _bodies;

    public NpcDoorService(
        ISectorService sectors,
        ITileDataService tiles,
        IItemScriptService scripts,
        IMobileTemplateService templates,
        IDataLoaderService data
    )
    {
        _sectors = sectors;
        _tiles = tiles;
        _scripts = scripts;
        _templates = templates;
        _bodies = new(() => data.GetEntities<BodyContent>().ToDictionary(body => (int)body.Body.Value, body => body.Type));
    }

    public bool OpensDoors(MobileEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (!npc.IsNpc)
        {
            return false;
        }

        if (npc.TemplateId is { } id && _templates.TryGet(id, out var template) && template.OpensDoors is { } opens)
        {
            return opens;
        }

        // As ModernUO's CanOpenDoors: every body but an animal's or a sea creature's.
        return _bodies.Value.GetValueOrDefault(npc.Body, BodyType.Monster) is BodyType.Human or BodyType.Monster;
    }

    public bool TryOpen(MobileEntity npc, DirectionType direction)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (!OpensDoors(npc))
        {
            return false;
        }

        var ahead = npc.Location + direction;
        var items = _sectors.GetItemsAt(npc.Map, ahead.X, ahead.Y);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            if (item.GroundLocation is { } spot &&
                _tiles.TryGetItem(item.ItemId, out var tile) &&
                Doors.IsDoor(tile) &&
                spot.Z + tile.Height > npc.Location.Z &&
                npc.Location.Z + MoverHeight > spot.Z &&
                Doors.CanBeOpened(item) &&
                _scripts.HasScript(item))
            {
                _scripts.Queue(item, OpenFunction, (long)npc.Id.Value);

                return true;
            }
        }

        return false;
    }
}
