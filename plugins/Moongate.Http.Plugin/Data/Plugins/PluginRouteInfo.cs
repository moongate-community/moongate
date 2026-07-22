namespace Moongate.Http.Plugin.Data.Plugins;

/// <summary>
/// One mapped route, as the inspector reads it off the routing table. <see cref="Policy" /> is null when
/// the route is open — either because nothing guards it, or because <c>AllowAnonymous</c> overrides what
/// does.
/// </summary>
public record PluginRouteInfo(string Method, string Path, string? Policy);
