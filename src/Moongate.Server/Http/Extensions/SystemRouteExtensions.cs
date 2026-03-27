using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Server.Data.Version;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.Server.Utils;

namespace Moongate.Server.Http.Extensions;

internal static class SystemRouteExtensions
{
    public static IEndpointRouteBuilder MapSystemRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        var systemGroup = endpoints.MapGroup(string.Empty).WithTags("System");

        if (!context.IsUiEnabled)
        {
            endpoints.MapGet("/", HandleRoot)
                     .WithName("Root")
                     .WithSummary("Returns service availability.")
                     .Produces<string>(StatusCodes.Status200OK, "text/plain");
        }

        systemGroup.MapGet("/health", HandleHealth)
                   .WithName("Health")
                   .WithSummary("Returns health probe status.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain");

        systemGroup.MapGet(
                       "/metrics",
                       (CancellationToken cancellationToken) => HandleMetrics(context, cancellationToken)
                   )
                   .WithName("Metrics")
                   .WithSummary("Returns Prometheus metrics.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain")
                   .Produces<string>(StatusCodes.Status404NotFound, "text/plain")
                   .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

        systemGroup.MapGet("/api/version", HandleServerVersion)
                   .WithName("ServerVersion")
                   .WithSummary("Returns running server version metadata.")
                   .Produces<MoongateHttpServerVersion>(StatusCodes.Status200OK, "application/json");

        systemGroup.MapGet(
                       "/api/branding",
                       () => Results.Json(context.Branding, MoongateHttpJsonContext.Default.MoongateHttpBranding)
                   )
                   .WithName("Branding")
                   .WithSummary("Returns public branding metadata for login pages.")
                   .Produces<MoongateHttpBranding>(StatusCodes.Status200OK, "application/json");

        return endpoints;
    }

    private static IResult HandleHealth(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return TypedResults.Text("ok");
    }

    private static IResult HandleMetrics(MoongateHttpRouteContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (context.MetricsHttpSnapshotFactory is null)
        {
            return TypedResults.Text("metrics endpoint is not configured", statusCode: StatusCodes.Status404NotFound);
        }

        var snapshot = context.MetricsHttpSnapshotFactory.CreateSnapshot();

        if (snapshot is null)
        {
            return TypedResults.Text(
                "metrics are currently unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var payload = MoongateHttpService.BuildPrometheusPayload(snapshot);

        return TypedResults.Text(payload, "text/plain; version=0.0.4", Encoding.UTF8, StatusCodes.Status200OK);
    }

    private static IResult HandleRoot()
        => TypedResults.Text("Moongate HTTP Service is running.");

    private static IResult HandleServerVersion(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var response = new MoongateHttpServerVersion
        {
            Version = VersionUtils.Version,
            Codename = VersionUtils.Codename
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpServerVersion);
    }
}
