using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>map</c> Lua module: shows map items to players and sets the area they show and the course of pins plotted
///     on them; <c>map.display(user, serial)</c>.
/// </summary>
[ScriptModule("map", "Shows map items to players and sets the area they show and their course of pins.")]
public sealed class MapModule
{
    // The facets a map may show: Felucca to Ter Mur.
    private const int LastFacet = 5;

    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly ISessionService _sessions;
    private readonly IMapDisplayService _maps;
    private readonly IItemHandlingService _handling;

    public MapModule(
        IItemService items,
        IItemTemplateService templates,
        ISessionService sessions,
        IMapDisplayService maps,
        IItemHandlingService handling
    )
    {
        _items = items;
        _templates = templates;
        _sessions = sessions;
        _maps = maps;
        _handling = handling;
    }

    /// <summary>
    ///     Opens the map item for the player; <c>map.display(user, serial)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens the map item for the player: its area, its course of pins and whether the player may change it. False for a player not in the world, an unknown item, an item with no area (a blank map), or a map of a facet other than Felucca and Trammel to a client older than 7.0.13. It does not check the distance: a script does."
    )]
    public bool Display(long player, long serial)
    {
        return player is > 0 and <= uint.MaxValue &&
               _sessions.TryGetByCharacterId(new Serial((uint)player), out var session) &&
               TryGetItem(serial, out var map) &&
               _maps.Display(session, map);
    }

    /// <summary>
    ///     Sets the area a map item shows; <c>map.set_bounds(serial, 1092, 1396, 1736, 1924, 200, 200, 0)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets the area a map item shows: its north-west corner x1, y1 and south-east corner x2, y2 in tiles (kept within 0 to 5119 and 0 to 4095), the size of its drawing in pixels (1 to 800, 200 for a small map) and the facet (0 Felucca, 1 Trammel, 2 Ilshenar, 3 Malas, 4 Tokuno, 5 Ter Mur). False for an unknown or held item, corners not north-west of each other, no drawing or an unknown facet."
    )]
    public bool SetBounds(long serial, int x1, int y1, int x2, int y2, int width, int height, int facet)
    {
        if (x1 >= x2 || y1 >= y2 || width < 1 || height < 1 || facet is < 0 or > LastFacet || !TryGetFree(serial, out var map))
        {
            return false;
        }

        MapItemProps.SetArea(map, new(x1, y1, x2, y2, width, height, facet));

        return true;
    }

    /// <summary>
    ///     The area a map item shows; <c>local b = map.bounds(serial)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The area a map item shows, as { x1, y1, x2, y2, width, height, facet }: its own, else the one of its preset template. Nil for an unknown item or one with no area."
    )]
    public LuaValue Bounds(long serial)
    {
        if (!TryGetItem(serial, out var map) || !TryGetArea(map, out var area))
        {
            return LuaValue.Nil;
        }

        var table = new LuaTable();
        table["x1"] = area.X1;
        table["y1"] = area.Y1;
        table["x2"] = area.X2;
        table["y2"] = area.Y2;
        table["width"] = area.Width;
        table["height"] = area.Height;
        table["facet"] = area.Facet;

        return table;
    }

    /// <summary>
    ///     The course of a map item; <c>for _, pin in ipairs(map.pins(serial)) do ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The course plotted on a map item, as an array of { x, y } in pixels of its drawing, in order. Empty for an unknown item or one with no course."
    )]
    public LuaTable Pins(long serial)
    {
        var list = new LuaTable();

        if (!TryGetItem(serial, out var map))
        {
            return list;
        }

        var index = 1;

        foreach (var (x, y) in MapItemProps.GetPins(map))
        {
            var pin = new LuaTable();
            pin["x"] = x;
            pin["y"] = y;
            list[index++] = pin;
        }

        return list;
    }

    /// <summary>
    ///     Replaces the course of a map item; <c>map.set_pins(serial, { { x = 10, y = 20 } })</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Replaces the course of a map item with an array of { x, y } in pixels of its drawing; an empty array clears it. False for an unknown or held item, more than 50 pins, or a pin without numbers x and y."
    )]
    public bool SetPins(long serial, LuaTable pins)
    {
        if (pins.ArrayLength > MapItemProps.MaxPins || !TryGetFree(serial, out var map))
        {
            return false;
        }

        var course = new List<(int X, int Y)>();

        for (var index = 1; index <= pins.ArrayLength; index++)
        {
            if (!pins[index].TryRead<LuaTable>(out var pin) ||
                !pin["x"].TryRead<double>(out var x) ||
                !pin["y"].TryRead<double>(out var y))
            {
                return false;
            }

            course.Add(((int)x, (int)y));
        }

        MapItemProps.SetPins(map, course);

        return true;
    }

    /// <summary>
    ///     Adds a pin at a tile of the world to the course of a map item; <c>map.add_world_pin(serial, x, y)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Adds to the course of a map item a pin at the tile x, y of the world, where its drawing shows it, as a treasure map marks its chest. False for an unknown or held item, one with no area, a tile outside its area or a course of 50 pins already."
    )]
    public bool AddWorldPin(long serial, int x, int y)
    {
        if (!TryGetFree(serial, out var map) || !TryGetArea(map, out var area))
        {
            return false;
        }

        var pins = MapItemProps.GetPins(map).ToList();

        if (x < area.X1 || x > area.X2 || y < area.Y1 || y > area.Y2 || pins.Count >= MapItemProps.MaxPins)
        {
            return false;
        }

        pins.Add(MapItemProps.WorldToPixel(area, x, y));
        MapItemProps.SetPins(map, pins);

        return true;
    }

    /// <summary>
    ///     Sets whether the course of a map item may be changed; <c>map.set_editable(serial, true)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets whether the course of a map item may be changed, as the lock of its window does. False for an unknown or held item."
    )]
    public bool SetEditable(long serial, bool editable)
    {
        if (!TryGetFree(serial, out var map))
        {
            return false;
        }

        MapItemProps.SetEditable(map, editable);

        return true;
    }

    /// <summary>
    ///     Sets whether no player may ever change the course of a map item; <c>map.set_protected(serial, true)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets whether no player may ever change the course of a map item, as for a map a quest gives. False for an unknown or held item."
    )]
    public bool SetProtected(long serial, bool guarded)
    {
        if (!TryGetFree(serial, out var map))
        {
            return false;
        }

        MapItemProps.SetProtected(map, guarded);

        return true;
    }

    private bool TryGetItem(long serial, out ItemEntity item)
    {
        // Safe: only read when the method returns true.
        item = null!;

        return serial is > 0 and <= uint.MaxValue && _items.TryGet(new Serial((uint)serial), out item!);
    }

    // An item a player holds on the cursor still counts where it was taken from: it is not changed until it is dropped.
    private bool TryGetFree(long serial, out ItemEntity item)
    {
        return TryGetItem(serial, out item) && !_handling.IsHeld(item);
    }

    private bool TryGetArea(ItemEntity map, out MapArea area)
    {
        _templates.TryGet(map.TemplateId, out var template);

        return MapItemProps.TryGetArea(map, template, out area);
    }
}
