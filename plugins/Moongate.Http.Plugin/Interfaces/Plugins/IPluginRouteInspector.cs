using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Plugins;

namespace Moongate.Http.Plugin.Interfaces.Plugins;

/// <summary>Reads the routing table and attributes every route to the assembly that declares it.</summary>
public interface IPluginRouteInspector
{
    /// <summary>
    /// The mapped routes, keyed by the simple name of the assembly declaring each handler. Routes whose
    /// handler cannot be identified — the ones the framework maps for itself — are grouped under an empty
    /// key rather than dropped.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<PluginRouteInfo>> RoutesByAssembly(EndpointDataSource endpoints);
}
