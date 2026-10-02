using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>moongates</c> Lua module: the public moongates of the maps the server loads, from
///     <c>data/moongates.toml</c>, for the script that shows them to a traveller; <c>moongates.facets()</c>.
/// </summary>
[ScriptModule("moongates", "The public moongates of the loaded maps, from data/moongates.toml: the maps and the cities a gate can take a traveller to.")]
public sealed class MoongatesModule
{
    private readonly IPublicMoongateService _moongates;

    public MoongatesModule(IPublicMoongateService moongates)
    {
        _moongates = moongates;
    }

    /// <summary>
    ///     Gets the maps that have public moongates, in file order; <c>moongates.facets()</c>. Each is
    ///     <c>{ map, cliloc, selected_cliloc, destinations }</c>, and each destination
    ///     <c>{ name, cliloc, x, y, z }</c>.
    /// </summary>
    [ScriptFunction(helpText: "The maps with public moongates, as an array of { map, cliloc, selected_cliloc, destinations }; each destination is { name, cliloc, x, y, z }.")]
    public LuaTable Facets()
    {
        var facets = new LuaTable();
        var index = 1;

        foreach (var facet in _moongates.GetFacets())
        {
            var destinations = new LuaTable();

            for (var position = 0; position < facet.Destination.Count; position++)
            {
                var destination = facet.Destination[position];
                var entry = new LuaTable();
                entry["name"] = destination.Name;
                entry["cliloc"] = destination.Cliloc;
                entry["x"] = destination.Location.X;
                entry["y"] = destination.Location.Y;
                entry["z"] = destination.Location.Z;
                destinations[position + 1] = entry;
            }

            var table = new LuaTable();
            table["map"] = (int)facet.Map;
            table["cliloc"] = facet.Cliloc;
            table["selected_cliloc"] = facet.SelectedCliloc;
            table["destinations"] = destinations;
            facets[index++] = table;
        }

        return facets;
    }
}
