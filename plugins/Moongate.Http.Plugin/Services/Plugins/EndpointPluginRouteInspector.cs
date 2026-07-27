using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Plugins;
using Moongate.Http.Plugin.Interfaces.Plugins;

namespace Moongate.Http.Plugin.Services.Plugins;

/// <summary>
/// Attributes routes to plugins through the routing table's own metadata.
/// <para>
/// Minimal APIs record the handler's <see cref="MethodInfo" /> against each endpoint, and the type
/// declaring it names an assembly — which is enough to say which plugin a route belongs to without any
/// endpoint having to declare an owner. It holds for lambda handlers too: the compiler puts the closure
/// in the same assembly as the code that wrote it.
/// </para>
/// </summary>
public sealed class EndpointPluginRouteInspector : IPluginRouteInspector
{
    /// <summary>Stands in for the verb when an endpoint declares none.</summary>
    private const string AnyMethod = "*";

    public IReadOnlyDictionary<string, IReadOnlyList<PluginRouteInfo>> RoutesByAssembly(EndpointDataSource endpoints)
    {
        var grouped = new Dictionary<string, List<PluginRouteInfo>>(StringComparer.Ordinal);

        foreach (var endpoint in endpoints.Endpoints.OfType<RouteEndpoint>())
        {
            var assembly = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly.GetName().Name
                           ?? string.Empty;

            if (!grouped.TryGetValue(assembly, out var routes))
            {
                routes = [];
                grouped[assembly] = routes;
            }

            var path = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
            var policy = PolicyOf(endpoint);

            foreach (var method in MethodsOf(endpoint))
            {
                routes.Add(new(method, path, policy));
            }
        }

        return grouped.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<PluginRouteInfo>)entry.Value,
            StringComparer.Ordinal
        );
    }

    private static IEnumerable<string> MethodsOf(Endpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;

        return methods is null || methods.Count == 0 ? [AnyMethod] : methods;
    }

    /// <summary>
    /// The policy guarding an endpoint, or null when it is open. <c>AllowAnonymous</c> is checked first
    /// because it wins at runtime over any policy present alongside it.
    /// </summary>
    private static string? PolicyOf(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return null;
        }

        return endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Select(data => data.Policy ?? data.Roles)
            .FirstOrDefault(value => !string.IsNullOrEmpty(value));
    }
}
