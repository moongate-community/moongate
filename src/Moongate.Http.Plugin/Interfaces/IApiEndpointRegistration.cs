using Microsoft.AspNetCore.Routing;

namespace Moongate.Http.Plugin.Interfaces;

/// <summary>
/// A group of REST endpoints. Implementations are collected from DI and applied by the HTTP server at
/// startup, so adding endpoints never means touching the server itself — the packet-side and event-side
/// registrations work the same way.
/// </summary>
public interface IApiEndpointRegistration
{
    /// <summary>Maps this group's routes onto the application.</summary>
    void Register(IEndpointRouteBuilder routes);
}
