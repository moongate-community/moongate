using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>locations</c> Lua module: the named places of the maps the server loads, from
///     <c>data/locations.toml</c>, as the go gump lists them; <c>locations.node("felucca/dungeons")</c>,
///     <c>locations.find("covetous entrance", MapType.Felucca)</c>.
/// </summary>
[ScriptModule("locations", "The named places of the loaded maps, from data/locations.toml: the tree of maps and categories the go gump lists, and the places a name finds.")]
public sealed class LocationsModule
{
    private readonly ILocationService _locations;

    public LocationsModule(ILocationService locations)
    {
        _locations = locations;
    }

    /// <summary>
    ///     Gets one level of the places; <c>locations.node("")</c> for the maps,
    ///     <c>locations.node("felucca/dungeons")</c> for a category. It is
    ///     <c>{ path, name, categories, locations }</c>: each category <c>{ name, path }</c>, each location
    ///     <c>{ name, category, map, x, y, z }</c>.
    /// </summary>
    [ScriptFunction(helpText: "One level of the named places as { path, name, categories, locations }: \"\" for the maps, then map/category/..., in any case; each category is { name, path }, each location { name, category, map, x, y, z }. nil for an unknown path.")]
    public LuaValue Node(string path = "")
    {
        if (_locations.GetNode(path) is not { } node)
        {
            return LuaValue.Nil;
        }

        var categories = new LuaTable();

        for (var index = 0; index < node.Categories.Count; index++)
        {
            var category = new LuaTable();
            category["name"] = node.Categories[index];
            category["path"] = node.Path.Length == 0 ? node.Categories[index] : node.Path + "/" + node.Categories[index];
            categories[index + 1] = category;
        }

        var table = new LuaTable();
        table["path"] = node.Path;
        table["name"] = node.Name;
        table["categories"] = categories;
        table["locations"] = Places(node.Locations);

        return table;
    }

    /// <summary>
    ///     Finds the places a text names, those of <paramref name="map" /> first;
    ///     <c>locations.find("covetous entrance", MapType.Felucca)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The places the text names, as an array of { name, category, map, x, y, z }: a name, or the last words of the categories and the name, such as \"covetous entrance\"; a category alone gives its first place. Those of the given map when any fits, else those of the other maps.")]
    public LuaTable Find(string text, MapType map)
    {
        return Places(_locations.Find(text, map));
    }

    private static LuaTable Places(IReadOnlyList<NamedLocation> places)
    {
        var table = new LuaTable();

        for (var index = 0; index < places.Count; index++)
        {
            var place = places[index];
            var entry = new LuaTable();
            entry["name"] = place.Name;
            entry["category"] = place.Category;
            entry["map"] = (int)place.Map;
            entry["x"] = place.Location.X;
            entry["y"] = place.Location.Y;
            entry["z"] = place.Location.Z;
            table[index + 1] = entry;
        }

        return table;
    }
}
