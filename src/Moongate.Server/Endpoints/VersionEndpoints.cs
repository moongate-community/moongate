using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Server.Data.Api;
using Moongate.Server.Data.Config;
using SquidStd.Core.Utils;

namespace Moongate.Server.Endpoints;

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
        routes.MapGet(
                  "/api/v1/version",
                  () => Results.Ok(
                      new VersionResponse(_config.ShardName, VersionUtils.GetVersion(typeof(VersionEndpoints).Assembly))
                  )
              )
              .WithName("GetVersion")
              .WithTags("version")
              .AllowAnonymous();
    }
}
