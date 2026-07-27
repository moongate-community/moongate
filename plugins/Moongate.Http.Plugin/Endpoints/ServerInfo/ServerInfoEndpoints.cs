using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.ServerInfo;
using Moongate.Http.Plugin.Interfaces.Assets;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Registration;
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
    private readonly IRegistrationReadinessService _registrationReadiness;

    public ServerInfoEndpoints(
        MoongateConfig config,
        IServerSettingsService settings,
        IServerAssetFileStore assets,
        IRegistrationReadinessService registrationReadiness
    )
    {
        _config = config;
        _settings = settings;
        _assets = assets;
        _registrationReadiness = registrationReadiness;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/server-info", Get)
            .WithName("GetServerInfo")
            .WithTags("server-info")
            .Produces<ServerInfoResponse>()
            .AllowAnonymous();
        routes.MapGet("/api/v1/server-info/assets/{slot}", GetAsset)
            .WithName("GetServerAsset")
            .WithTags("server-info")

            // Binary, but no content type stated: the slot decides it. An operator's logo may be a PNG,
            // an SVG or an ICO, and naming one here would document a promise the route does not make.
            .Produces<byte[]>(StatusCodes.Status200OK)
            .AllowAnonymous();
    }

    internal static ServerInfoResponse ToResponse(
        string shardName,
        ServerSettingsEntity settings,
        IRegistrationReadinessService registrationReadiness
    )
    {
        var assets = settings.Assets.Keys.ToDictionary(
            slot => slot,
            slot => $"/api/v1/server-info/assets/{slot.ToLowerInvariant()}"
        );
        var readiness = registrationReadiness.Evaluate(settings.Contacts.Website);

        return new(
            shardName,
            settings.Description,
            settings.Tagline,
            new(settings.Contacts.Website, settings.Contacts.Email, settings.Contacts.Discord),
            settings.RegistrationEnabled && readiness.Ready,
            assets
        );
    }

    /// <summary>Returns the shard's public profile: name, description, contacts, asset URLs and whether registration is open.</summary>
    private IResult Get()
        => Results.Ok(ToResponse(_config.ShardName, _settings.Get(), _registrationReadiness));

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
