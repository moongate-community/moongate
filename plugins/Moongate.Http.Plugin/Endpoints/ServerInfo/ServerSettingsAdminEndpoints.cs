using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.ServerInfo;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces.Assets;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Assets;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Http.Plugin.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Http.Plugin.Endpoints.ServerInfo;

/// <summary>Staff-only administration of the shard's public settings and visual assets.</summary>
public sealed class ServerSettingsAdminEndpoints : IApiEndpointRegistration
{
    private readonly IServerSettingsService _settings;
    private readonly IServerAssetFileStore _assets;
    private readonly MoongateHttpConfig _config;

    public ServerSettingsAdminEndpoints(IServerSettingsService settings, IServerAssetFileStore assets, MoongateHttpConfig config)
    {
        _settings = settings;
        _assets = assets;
        _config = config;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/server-settings")
                          .WithTags("server-settings")
                          .RequireAuthorization(HttpServerService.AdminPolicy);

        group.MapGet("/", Get).WithName("GetServerSettings").Produces<ServerSettingsResponse>();
        group.MapPut("/", Update).WithName("UpdateServerSettings").Produces<ServerSettingsResponse>();
        group.MapPost("/assets/{slot}", UploadAsset)
             .WithName("UploadServerAsset")
             .DisableAntiforgery()
             .Produces<ServerSettingsResponse>();
        group.MapDelete("/assets/{slot}", DeleteAsset)
             .WithName("DeleteServerAsset")
             .Produces(StatusCodes.Status204NoContent);
    }

    internal static ServerSettingsResponse ToResponse(ServerSettingsEntity settings)
        => new(
            settings.Description,
            settings.Tagline,
            new(settings.Contacts.Website, settings.Contacts.Email, settings.Contacts.Discord),
            settings.RegistrationEnabled,
            settings.Assets.Keys.ToDictionary(slot => slot, slot => $"/api/v1/server-info/assets/{slot.ToLowerInvariant()}")
        );

    /// <summary>Returns the full server settings.</summary>
    private IResult Get()
        => Results.Ok(ToResponse(_settings.Get()));

    /// <summary>Updates the server settings; every field is optional and an omitted one is left unchanged.</summary>
    private IResult Update(UpdateServerSettingsRequest request)
    {
        _settings.Update(
            new ServerSettingsUpdate
            {
                Description = request.Description,
                Tagline = request.Tagline,
                RegistrationEnabled = request.RegistrationEnabled,
                Contacts = request.Contacts is null
                               ? null
                               : new ServerContacts
                               {
                                   Website = request.Contacts.Website,
                                   Email = request.Contacts.Email,
                                   Discord = request.Contacts.Discord
                               }
            }
        );

        return Results.Ok(ToResponse(_settings.Get()));
    }

    /// <summary>Uploads the image for a slot (logo, favicon or banner), replacing any previous one.</summary>
    /// <remarks>Answers 400 for an unknown slot, 415 for a non-image type, and 413 when the file is too large.</remarks>
    private async Task<IResult> UploadAsset(string slot, IFormFile file)
    {
        if (!Enum.TryParse<ServerAssetSlotType>(slot, ignoreCase: true, out var parsed))
        {
            return Results.Problem($"'{slot}' is not an asset slot.", statusCode: StatusCodes.Status400BadRequest);
        }

        var validation = AssetUploadValidation.Validate(file.ContentType, file.Length, _config.MaxAssetUploadBytes);

        if (!validation.Ok)
        {
            return validation.Error == AssetValidationError.TooLarge
                       ? Results.Problem("Asset exceeds the maximum upload size.", statusCode: StatusCodes.Status413PayloadTooLarge)
                       : Results.Problem("Unsupported asset content-type.", statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        await using (var stream = file.OpenReadStream())
        {
            await _assets.SaveAsync(parsed, validation.Extension!, stream);
        }

        _settings.SetAsset(parsed, new ServerAssetMeta { FileName = $"{parsed}.{validation.Extension}", ContentType = file.ContentType });

        return Results.Ok(ToResponse(_settings.Get()));
    }

    /// <summary>Removes the image for a slot.</summary>
    private IResult DeleteAsset(string slot)
    {
        if (!Enum.TryParse<ServerAssetSlotType>(slot, ignoreCase: true, out var parsed))
        {
            return Results.Problem($"'{slot}' is not an asset slot.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (_settings.Get().Assets.TryGetValue(parsed.ToString(), out var meta))
        {
            _assets.Delete(meta.FileName);
            _settings.ClearAsset(parsed);
        }

        return Results.NoContent();
    }
}
