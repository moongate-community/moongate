using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.Plugins;
using Moongate.Http.Plugin.Data.Plugins;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Plugins;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Data.Plugins;
using Moongate.Server.Abstractions.Interfaces.Plugins;

namespace Moongate.Http.Plugin.Endpoints.Plugins;

/// <summary>Staff-only view of which plugins this shard activated and what each one serves.</summary>
public sealed class PluginAdminEndpoints : IApiEndpointRegistration
{
    /// <summary>The synthetic entry carrying routes no plugin declares.</summary>
    private const string HostId = "moongate.host";

    private readonly IPluginCatalog _catalog;
    private readonly IPluginRouteInspector _inspector;

    public PluginAdminEndpoints(IPluginCatalog catalog, IPluginRouteInspector inspector)
    {
        _catalog = catalog;
        _inspector = inspector;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/admin/plugins", Get)
                 .WithName("GetAdminPlugins")
                 .WithTags("admin")
                 .RequireAuthorization(HttpServerService.AdminPolicy);

    /// <summary>Lists the plugins this shard activated, each with the HTTP routes it declares.</summary>
    /// <remarks>
    /// Routes belonging to no plugin — the API reference and framework middleware — are gathered under a
    /// final synthetic entry with id <c>moongate.host</c>, so that everything the shard serves appears
    /// somewhere. <c>policy</c> names the authorization policy guarding a route, and is null when the
    /// route is open.
    /// </remarks>

    // EndpointDataSource arrives as a parameter rather than a constructor dependency: it does not exist in
    // the container while plugins are being configured, only once the web application has been built.
    //
    // [FromServices] is what keeps it out of the OpenAPI document. Minimal APIs resolve it from the
    // container either way, but the API explorer does not infer that, and describes it as a bindable
    // parameter — dragging Endpoint, RequestDelegate, MethodInfo, Assembly, Type and the rest of the
    // reflection graph in as schema components, roughly thirty of them, for a route that takes no input.
    private IResult Get([FromServices] EndpointDataSource endpoints)
    {
        var byAssembly = _inspector.RoutesByAssembly(endpoints);
        var catalogued = _catalog.Plugins.Select(plugin => plugin.AssemblyName).ToHashSet(StringComparer.Ordinal);

        var plugins = _catalog.Plugins.Select(plugin => ToResponse(plugin, byAssembly)).ToList();

        var unattributed = byAssembly.Where(entry => !catalogued.Contains(entry.Key))
                                     .SelectMany(entry => entry.Value)
                                     .Select(ToResponse)
                                     .ToList();

        plugins.Add(Host(unattributed));

        return Results.Ok(plugins);
    }

    private static PluginInfoResponse ToResponse(
        PluginDescriptor plugin,
        IReadOnlyDictionary<string, IReadOnlyList<PluginRouteInfo>> byAssembly
    )
    {
        var routes = byAssembly.TryGetValue(plugin.AssemblyName, out var declared)
            ? declared.Select(ToResponse).ToList()
            : [];

        return new(
            plugin.Id,
            plugin.Name,
            plugin.Version,
            plugin.Author,
            plugin.Description,
            plugin.AssemblyName,
            plugin.IsExternal,
            routes
        );
    }

    private static PluginRouteResponse ToResponse(PluginRouteInfo route)
        => new(route.Method, route.Path, route.Policy);

    /// <summary>
    /// The catch-all entry. Its version is this plugin's own: there is no host assembly to read here,
    /// because the HTTP plugin never references Moongate.Server.
    /// </summary>

    // System.Version is qualified because Moongate.Http.Plugin.Endpoints.Version is a sibling namespace of
    // this one, and an unqualified Version binds to it rather than to the type.
    private static PluginInfoResponse Host(IReadOnlyList<PluginRouteResponse> routes)
        => new(
            HostId,
            "Host & framework",
            typeof(PluginAdminEndpoints).Assembly.GetName().Version ?? new System.Version(0, 0),
            "moongate",
            "Routes no plugin declares: the API reference and framework middleware.",
            string.Empty,
            false,
            routes
        );
}
