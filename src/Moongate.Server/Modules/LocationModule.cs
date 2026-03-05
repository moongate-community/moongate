using Moongate.Server.Data.World;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Internal.World;
using Moongate.Server.Interfaces.Services.World;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("location", "Provides read access to world locations loaded in the catalog.")]
public sealed class LocationModule
{
    private static bool _isLuaLocationProxyTypeRegistered;
    private readonly ILocationCatalogService _locationCatalogService;

    public LocationModule(ILocationCatalogService locationCatalogService)
    {
        _locationCatalogService = locationCatalogService;
    }

    [ScriptFunction("count", "Returns the total number of loaded locations.")]
    public int Count()
        => _locationCatalogService.GetAllLocations().Count;

    [ScriptFunction("get", "Gets a location by Lua index (1-based), or nil when out of range.")]
    public LuaLocationProxy? Get(int index)
    {
        if (index <= 0)
        {
            return null;
        }

        var locations = _locationCatalogService.GetAllLocations();
        var zeroIndex = index - 1;

        if (zeroIndex >= locations.Count)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();

        return new(locations[zeroIndex]);
    }

    [ScriptFunction("find", "Finds the first location by name (case-insensitive), or nil.")]
    public LuaLocationProxy? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var locations = _locationCatalogService.GetAllLocations();
        var requestedName = name.Trim();
        var found = false;
        WorldLocationEntry match = default;

        foreach (var entry in locations)
        {
            if (!string.Equals(entry.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            match = entry;
            found = true;
            break;
        }

        if (!found)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();

        return new(match);
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaLocationProxyTypeRegistered)
        {
            return;
        }

        var type = typeof(LuaLocationProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaLocationProxyTypeRegistered = true;
    }
}
