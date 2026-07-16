using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Server.Data.Api;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Accounts;
using SquidStd.Core.Utils;

namespace Moongate.Server.Endpoints;

/// <summary>Staff-only shard administration.</summary>
public sealed class AdminEndpoints : IApiEndpointRegistration
{
    private readonly MoongateConfig _config;
    private readonly ISessionManager _sessions;

    public AdminEndpoints(MoongateConfig config, ISessionManager sessions)
    {
        _config = config;
        _sessions = sessions;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/admin/status", Status)
              .WithName("GetAdminStatus")
              .WithTags("admin")
              .RequireAuthorization(HttpServerService.AdminPolicy);
    }

    /// <summary>
    /// A snapshot of the shard. <see cref="ISessionManager.Count" /> is backed by a ConcurrentDictionary,
    /// so it is safe to read from an ASP.NET thread; nothing here touches world state, which is
    /// single-writer on the game loop.
    /// </summary>
    private IResult Status()
        => Results.Ok(
            new AdminStatusResponse(
                _config.ShardName,
                VersionUtils.GetVersion(typeof(AdminEndpoints).Assembly),
                _sessions.Count
            )
        );
}
