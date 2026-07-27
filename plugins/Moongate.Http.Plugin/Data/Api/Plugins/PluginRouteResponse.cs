namespace Moongate.Http.Plugin.Data.Api.Plugins;

/// <summary>One HTTP route a plugin declares. <c>policy</c> is null when the route is open.</summary>
public record PluginRouteResponse(string Method, string Path, string? Policy);
