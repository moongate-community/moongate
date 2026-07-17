using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Data.Api;
using Moongate.Server.Abstractions.Data.Config;
using SquidStd.Core.Utils;

namespace Moongate.Http.Plugin.Endpoints;

/// <summary>
/// The shard's name and build. Deliberately unauthenticated: a launcher or the website checks
/// compatibility with it, and it reveals nothing an operator would want hidden.
/// </summary>
public sealed class VersionEndpoints : IApiEndpointRegistration
{
    private readonly MoongateConfig _config;

    public VersionEndpoints(MoongateConfig config)
    {
        _config = config;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        // A method group rather than a lambda: Swashbuckle reads the /// off the handler's method, and a
        // lambda has no method to read it from — the route would document itself as blank.
        routes.MapGet("/api/v1/version", Version)
              .WithName("GetVersion")
              .WithTags("version")
              .AllowAnonymous();
    }

    /// <summary>Reports the shard's name and build.</summary>
    /// <remarks>
    /// Open without a token, so a launcher or the website can check compatibility before anyone logs in.
    /// </remarks>
    private IResult Version()
        => Results.Ok(
            new VersionResponse(_config.ShardName, VersionUtils.GetVersion(typeof(VersionEndpoints).Assembly))
        );
}
