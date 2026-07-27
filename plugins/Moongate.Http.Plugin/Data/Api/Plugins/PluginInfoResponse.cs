namespace Moongate.Http.Plugin.Data.Api.Plugins;

/// <summary>
/// One activated plugin and everything it exposes over HTTP. <c>version</c> goes over the wire as a
/// string, e.g. <c>"0.1.0"</c>.
/// </summary>
public record PluginInfoResponse(
    string Id,
    string Name,
    // Qualified: Moongate.Http.Plugin.Data.Api.Version is a sibling namespace of this one, so an
    // unqualified Version binds to it rather than to the type.
    System.Version Version,
    string Author,
    string Description,
    string Assembly,
    bool IsExternal,
    IReadOnlyList<PluginRouteResponse> Routes
);
