using Moongate.Core.Primitives;
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

    // How many times in a row an NPC asks for a door before the step is taken as refused.
    private const int MaxTries = 3;

    // Above this many NPCs waiting at a door, the list is started again.
    private const int ForgetAbove = 256;

    private readonly ISectorService _sectors;
    private readonly ITileDataService _tiles;
    private readonly IItemScriptService _scripts;
    private readonly IItemTemplateService _itemTemplates;
    private readonly IMobileTemplateService _templates;
    private readonly Lazy<Dictionary<int, BodyType>> _bodies;

    // How many times in a row each NPC asked a door to open without a step since.
    private readonly Dictionary<Serial, int> _tries = [];

    public NpcDoorService(
        ISectorService sectors,
        ITileDataService tiles,
        IItemScriptService scripts,
        IItemTemplateService itemTemplates,
        IMobileTemplateService templates,
        IDataLoaderService data
    )
    {
        _sectors = sectors;
        _tiles = tiles;
        _scripts = scripts;
        _itemTemplates = itemTemplates;
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

        var tries = _tries.GetValueOrDefault(npc.Id);

        if (tries >= MaxTries || !OpensDoors(npc))
        {
            return false;
        }

        var ahead = npc.Location + direction;

        // A step along a diagonal is refused by what stands on either cell beside it, so a door there is in the way too.
        var asked = TryOpenAt(npc, ahead.X, ahead.Y) ||
                    ahead.X != npc.Location.X &&
                    ahead.Y != npc.Location.Y &&
                    (TryOpenAt(npc, ahead.X, npc.Location.Y) || TryOpenAt(npc, npc.Location.X, ahead.Y));

        if (!asked)
        {
            return false;
        }

        // The NPCs that are gone are forgotten as the list is used.
        if (_tries.Count > ForgetAbove)
        {
            _tries.Clear();
        }

        _tries[npc.Id] = tries + 1;

        return true;
    }

    public void Moved(MobileEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        _tries.Remove(npc.Id);
    }

    private bool TryOpenAt(MobileEntity npc, int x, int y)
    {
        var items = _sectors.GetItemsAt(npc.Map, x, y);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            if (item.GroundLocation is { } spot &&
                _tiles.TryGetItem(item.ItemId, out var tile) &&
                Doors.IsDoor(tile) &&
                spot.Z + tile.Height > npc.Location.Z &&
                npc.Location.Z + MoverHeight > spot.Z &&
                Doors.CanBeOpened(item, _itemTemplates))
            {
                _scripts.Queue(item, OpenFunction, (long)npc.Id.Value);

                return true;
            }
        }

        return false;
    }
}
