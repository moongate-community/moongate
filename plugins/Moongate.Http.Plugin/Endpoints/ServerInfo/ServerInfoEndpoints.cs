using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.ServerInfo;
using Moongate.Http.Plugin.Interfaces.Assets;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Http.Plugin.Endpoints.ServerInfo;

/// <summary>The public server profile: what a website or launcher reads about the shard.</summary>
public sealed class ServerInfoEndpoints : IApiEndpointRegistration
{
    private readonly MoongateConfig _config;
    private readonly IServerSettingsService _settings;
    private readonly IServerAssetFileStore _assets;

    public ServerInfoEndpoints(MoongateConfig config, IServerSettingsService settings, IServerAssetFileStore assets)
    {
        _config = config;
        _settings = settings;
        _assets = assets;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/server-info", Get).WithName("GetServerInfo").WithTags("server-info").AllowAnonymous();
        routes.MapGet("/api/v1/server-info/assets/{slot}", GetAsset)
              .WithName("GetServerAsset")
              .WithTags("server-info")
              .AllowAnonymous();
    }

    internal static ServerInfoResponse ToResponse(string shardName, ServerSettingsEntity settings)
    {
        var assets = settings.Assets.Keys.ToDictionary(
            slot => slot,
            slot => $"/api/v1/server-info/assets/{slot.ToLowerInvariant()}"
        );

        return new(
            shardName,
            settings.Description,
            new(settings.Contacts.Website, settings.Contacts.Email, settings.Contacts.Discord),
            settings.RegistrationEnabled,
            assets
        );
    }

    /// <summary>Returns the shard's public profile: name, description, contacts, asset URLs and whether registration is open.</summary>
    private IResult Get()
        => Results.Ok(ToResponse(_config.ShardName, _settings.Get()));

    /// <summary>Streams a visual asset (logo, favicon or banner) by slot.</summary>
    /// <remarks>Answers 400 for an unknown slot and 404 when the slot has no asset.</remarks>
    private IResult GetAsset(string slot)
    {
        if (!Enum.TryParse<ServerAssetSlotType>(slot, ignoreCase: true, out var parsed))
        {
            return Results.Problem($"'{slot}' is not an asset slot.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!_settings.Get().Assets.TryGetValue(parsed.ToString(), out var meta))
        {
            return Results.Problem($"No {parsed} asset is set.", statusCode: StatusCodes.Status404NotFound);
        }

        if (_assets.TryOpen(meta.FileName) is not { } opened)
        {
            return Results.Problem($"No {parsed} asset is set.", statusCode: StatusCodes.Status404NotFound);
        }

        return Results.File(opened.stream, meta.ContentType);
    }
}
