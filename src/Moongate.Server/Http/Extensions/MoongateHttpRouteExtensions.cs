using Microsoft.AspNetCore.Routing;
using Moongate.Server.Http.Internal;

namespace Moongate.Server.Http.Extensions;

/// <summary>
/// Route mapping extensions for Moongate HTTP endpoints.
/// </summary>
internal static class MoongateHttpRouteExtensions
{
    public static IEndpointRouteBuilder MapMoongateHttpRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        endpoints.MapSystemRoutes(context);
        endpoints.MapAuthRoutes(context);
        endpoints.MapUserRoutes(context);
        endpoints.MapPortalRoutes(context);
        endpoints.MapSessionRoutes(context);
        endpoints.MapHelpTicketRoutes(context);
        endpoints.MapCommandRoutes(context);
        endpoints.MapItemTemplateRoutes(context);
        endpoints.MapMapRoutes(context);

        return endpoints;
    }
}
