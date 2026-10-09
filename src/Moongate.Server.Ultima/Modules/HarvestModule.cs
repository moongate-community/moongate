using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>harvest</c> Lua module: what is gathered from the world and runs out by area, the resources of
///     <c>data/harvest.toml</c>, such as the fish a fishing pole pulls out.
/// </summary>
[ScriptModule("harvest", "What is gathered from the world and runs out by area, such as the fish of the sea.")]
public sealed class HarvestModule
{
    private readonly IHarvestService _harvest;

    public HarvestModule(IHarvestService harvest)
    {
        _harvest = harvest;
    }

    /// <summary>
    ///     Whether <c>data/harvest.toml</c> has the resource; <c>harvest.has("fish")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether data/harvest.toml has a resource of that id, such as 'fish'.")]
    public bool Has(string resource)
    {
        return _harvest.Has(resource);
    }

    /// <summary>
    ///     How much of the resource is left in the area of a cell; <c>harvest.amount("fish", map, x, y)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "How much of the resource is left in the area of the cell x, y of the map; an area is full the first time it is asked for and again some time after the first take. Nil for an unknown resource or a cell below zero."
    )]
    public int? Amount(string resource, MapType map, int x, int y)
    {
        return _harvest.Amount(resource, map, x, y);
    }

    /// <summary>
    ///     The vein of the area of a cell; <c>harvest.vein("wood", map, x, y)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The vein of the area of the cell x, y of the map: the kind of the resource the area is of, such as 'oak' among the wood, one of the [[resource.vein]] of data/harvest.toml, drawn by weight each time the area fills. Nil for a resource without veins, an unknown resource or a cell below zero."
    )]
    public string? Vein(string resource, MapType map, int x, int y)
    {
        return _harvest.Vein(resource, map, x, y);
    }

    /// <summary>
    ///     Takes one of the resource from the area of a cell; <c>if harvest.take("fish", map, x, y) then ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Takes one of the resource from the area of the cell x, y of the map; false when the area has none left, for an unknown resource or a cell below zero."
    )]
    public bool Take(string resource, MapType map, int x, int y)
    {
        return _harvest.TryTake(resource, map, x, y);
    }
}
