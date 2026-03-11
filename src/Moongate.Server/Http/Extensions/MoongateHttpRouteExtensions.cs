using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using Moongate.Core.Types;
using Moongate.Server.Data.Version;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;
using SixLabors.ImageSharp.Formats.Png;

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

        if (context.JwtOptions.IsEnabled && context.AccountService is not null)
        {
            var authGroup = endpoints.MapGroup("/auth").WithTags("Auth");
            authGroup.MapPost(
                         "/login",
                         (MoongateHttpLoginRequest request, CancellationToken cancellationToken) =>
                             HandleLogin(context, request, cancellationToken)
                     )
                     .WithName("AuthLogin")
                     .WithSummary("Authenticates a user and returns a JWT token.")
                     .Accepts<MoongateHttpLoginRequest>("application/json")
                     .WithMetadata(
                         new ProducesResponseTypeMetadata(
                             StatusCodes.Status200OK,
                             typeof(MoongateHttpLoginResponse),
                             ["application/json"]
                         )
                     )
                     .WithMetadata(
                         new ProducesResponseTypeMetadata(StatusCodes.Status400BadRequest, typeof(string), ["text/plain"])
                     )
                     .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized));
        }

        if (context.AccountService is not null)
        {
            var usersGroup = endpoints.MapGroup("/api/users").WithTags("Users");

            if (context.JwtOptions.IsEnabled)
            {
                usersGroup.RequireAuthorization();
            }

            usersGroup.MapGet(
                          "/",
                          (CancellationToken cancellationToken) => HandleGetUsers(context, cancellationToken)
                      )
                      .WithName("UsersGetAll")
                      .WithSummary("Returns all users.")
                      .Produces<IReadOnlyList<MoongateHttpUser>>();

            usersGroup.MapGet(
                          "/{accountId}",
                          (string accountId, CancellationToken cancellationToken) =>
                              HandleGetUserById(context, accountId, cancellationToken)
                      )
                      .WithName("UsersGetById")
                      .WithSummary("Returns a user by account id.")
                      .Produces<MoongateHttpUser>()
                      .Produces(StatusCodes.Status404NotFound);

            usersGroup.MapPost(
                          "/",
                          (MoongateHttpCreateUserRequest request, CancellationToken cancellationToken) =>
                              HandleCreateUser(context, request, cancellationToken)
                      )
                      .WithName("UsersCreate")
                      .WithSummary("Creates a new user.")
                      .Accepts<MoongateHttpCreateUserRequest>("application/json")
                      .Produces<MoongateHttpUser>(StatusCodes.Status201Created)
                      .Produces(StatusCodes.Status400BadRequest)
                      .Produces(StatusCodes.Status409Conflict);

            usersGroup.MapPut(
                          "/{accountId}",
                          (
                              string accountId,
                              MoongateHttpUpdateUserRequest request,
                              CancellationToken cancellationToken
                          ) => HandleUpdateUser(context, accountId, request, cancellationToken)
                      )
                      .WithName("UsersUpdate")
                      .WithSummary("Updates a user by account id.")
                      .Accepts<MoongateHttpUpdateUserRequest>("application/json")
                      .Produces<MoongateHttpUser>()
                      .Produces(StatusCodes.Status400BadRequest)
                      .Produces(StatusCodes.Status404NotFound);

            usersGroup.MapDelete(
                          "/{accountId}",
                          (string accountId, CancellationToken cancellationToken) =>
                              HandleDeleteUser(context, accountId, cancellationToken)
                      )
                      .WithName("UsersDelete")
                      .WithSummary("Deletes a user by account id.")
                      .Produces(StatusCodes.Status204NoContent)
                      .Produces(StatusCodes.Status404NotFound);
        }

        if (context.AccountService is not null && context.CharacterService is not null)
        {
            var portalGroup = endpoints.MapGroup("/api/portal").WithTags("Portal");

            if (context.JwtOptions.IsEnabled)
            {
                portalGroup.RequireAuthorization();
            }

            portalGroup.MapGet(
                           "/me",
                           (ClaimsPrincipal user, CancellationToken cancellationToken) =>
                               HandleGetPortalMe(context, user, cancellationToken)
                       )
                       .WithName("PortalGetMe")
                       .WithSummary("Returns the authenticated player's account and characters.")
                       .Produces<MoongateHttpPortalAccount>()
                       .Produces(StatusCodes.Status401Unauthorized)
                       .Produces(StatusCodes.Status404NotFound);
        }

        if (context.GameNetworkSessionService is not null)
        {
            var sessionsGroup = endpoints.MapGroup("/api/sessions").WithTags("Sessions");

            if (context.JwtOptions.IsEnabled)
            {
                sessionsGroup.RequireAuthorization();
            }

            sessionsGroup.MapGet(
                             "/active",
                             (CancellationToken cancellationToken) => HandleGetActiveSessions(context, cancellationToken)
                         )
                         .WithName("SessionsGetActive")
                         .WithSummary("Returns currently active in-game sessions.")
                         .Produces<IReadOnlyList<MoongateHttpActiveSession>>();
        }

        if (context.CommandSystemService is not null)
        {
            var commandsGroup = endpoints.MapGroup("/api/commands").WithTags("Commands");

            if (context.JwtOptions.IsEnabled)
            {
                commandsGroup.RequireAuthorization();
            }

            commandsGroup.MapPost(
                             "/execute",
                             (
                                 MoongateHttpExecuteCommandRequest request,
                                 CancellationToken cancellationToken
                             ) => HandleExecuteCommand(context, request, cancellationToken)
                         )
                         .WithName("CommandsExecute")
                         .WithSummary("Executes a console command and returns final output lines.")
                         .Accepts<MoongateHttpExecuteCommandRequest>("application/json")
                         .Produces<MoongateHttpExecuteCommandResponse>()
                         .Produces(StatusCodes.Status400BadRequest);
        }

        if (context.ItemTemplateService is not null || context.ArtService is not null)
        {
            var itemTemplatesGroup = endpoints.MapGroup("/api/item-templates").WithTags("ItemTemplates");

            if (context.JwtOptions.IsEnabled)
            {
                itemTemplatesGroup.RequireAuthorization();
            }

            if (context.ItemTemplateService is not null)
            {
                itemTemplatesGroup.MapGet(
                                      "/",
                                      (
                                          int page,
                                          int pageSize,
                                          string? name,
                                          string? tag,
                                          CancellationToken cancellationToken
                                      ) => HandleGetItemTemplates(
                                          context,
                                          page,
                                          pageSize,
                                          name,
                                          tag,
                                          cancellationToken
                                      )
                                  )
                                  .WithName("ItemTemplatesGetPaged")
                                  .WithSummary("Returns paged item templates.")
                                  .Produces<MoongateHttpItemTemplatePage>()
                                  .Produces(StatusCodes.Status400BadRequest);

                itemTemplatesGroup.MapGet(
                                      "/{id}",
                                      (string id, CancellationToken cancellationToken) =>
                                          HandleGetItemTemplateById(context, id, cancellationToken)
                                  )
                                  .WithName("ItemTemplatesGetById")
                                  .WithSummary("Returns an item template by id.")
                                  .Produces<MoongateHttpItemTemplateDetail>()
                                  .Produces(StatusCodes.Status404NotFound);
            }

            if (context.ArtService is not null)
            {
                itemTemplatesGroup.MapGet(
                                      "/by-item-id/{itemId}/image",
                                      (string itemId, CancellationToken cancellationToken) =>
                                          HandleGetItemTemplateImageByItemId(context, itemId, cancellationToken)
                                  )
                                  .WithName("ItemTemplatesGetImageByItemId")
                                  .WithSummary("Returns item art image by item graphic id (0x....).")
                                  .Produces(StatusCodes.Status200OK, contentType: "image/png")
                                  .Produces(StatusCodes.Status400BadRequest)
                                  .Produces(StatusCodes.Status404NotFound);
            }
        }

        if (context.MapImageService is not null)
        {
            var mapsGroup = endpoints.MapGroup("/api/maps").WithTags("Maps");

            if (context.JwtOptions.IsEnabled)
            {
                mapsGroup.RequireAuthorization();
            }

            mapsGroup.MapGet(
                         "/{mapId}.png",
                         (int mapId, CancellationToken cancellationToken) =>
                             HandleGetMapImage(context, mapId, cancellationToken)
                     )
                     .WithName("MapsGetImage")
                     .WithSummary("Returns a radar-color PNG image of the specified map.")
                     .Produces(StatusCodes.Status200OK, contentType: "image/png")
                     .Produces(StatusCodes.Status404NotFound);
        }

        return endpoints;
    }

    private static string CreateJwtToken(
        MoongateHttpAuthenticatedUser user,
        DateTimeOffset expiresAtUtc,
        MoongateHttpJwtOptions options
    )
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("account_id", user.AccountId)
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            DateTime.UtcNow,
            expiresAtUtc.UtcDateTime,
            signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IResult HandleCreateUser(
        MoongateHttpRouteContext context,
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest("username and password are required");
        }

        if (!Enum.TryParse<AccountType>(request.Role, true, out var role))
        {
            return TypedResults.BadRequest("invalid role");
        }

        var created = context.AccountService!
                             .CreateAccountAsync(request.Username, request.Password, request.Email, role)
                             .GetAwaiter()
                             .GetResult();

        if (created is null)
        {
            return TypedResults.Conflict();
        }

        var user = MapAccountToHttpUser(created);

        return TypedResults.Created($"/api/users/{user.AccountId}", user);
    }

    private static IResult HandleDeleteUser(
        MoongateHttpRouteContext context,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        var parsedId = ParseAccountIdOrNull(accountId);

        if (!parsedId.HasValue)
        {
            return TypedResults.BadRequest("invalid accountId");
        }

        var deleted = context.AccountService!
                             .DeleteAccountAsync(parsedId.Value)
                             .GetAwaiter()
                             .GetResult();

        return deleted
                   ? TypedResults.NoContent()
                   : TypedResults.NotFound();
    }

    private static IResult HandleExecuteCommand(
        MoongateHttpRouteContext context,
        MoongateHttpExecuteCommandRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return TypedResults.BadRequest("command is required");
        }

        var lines = context.CommandSystemService!
                           .ExecuteCommandWithOutputAsync(
                               request.Command.Trim(),
                               CommandSourceType.Console,
                               null,
                               cancellationToken
                           )
                           .GetAwaiter()
                           .GetResult();

        var response = new MoongateHttpExecuteCommandResponse
        {
            Success = true,
            Command = request.Command.Trim(),
            OutputLines = lines,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpExecuteCommandResponse);
    }

    private static IResult HandleGetActiveSessions(MoongateHttpRouteContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var sessions = context.GameNetworkSessionService!.GetAll()
                              .Where(session => session.AccountId.Value != 0 && session.CharacterId.Value != 0)
                              .OrderBy(session => session.SessionId)
                              .Select(
                                  session =>
                                  {
                                      var account = context.AccountService
                                                           ?
                                                           .GetAccountAsync(session.AccountId)
                                                           .GetAwaiter()
                                                           .GetResult();

                                      return new MoongateHttpActiveSession
                                      {
                                          SessionId = session.SessionId,
                                          AccountId = session.AccountId.Value.ToString(),
                                          Username = account?.Username ?? string.Empty,
                                          AccountType = session.AccountType.ToString(),
                                          CharacterId = session.CharacterId.Value.ToString(),
                                          CharacterName = session.Character?.Name ?? string.Empty,
                                          MapId = session.Character?.MapId ?? 0,
                                          X = session.Character?.Location.X ?? 0,
                                          Y = session.Character?.Location.Y ?? 0
                                      };
                                  }
                              )
                              .ToList();

        return TypedResults.Ok((IReadOnlyList<MoongateHttpActiveSession>)sessions);
    }

    private static IResult HandleGetItemTemplateById(
        MoongateHttpRouteContext context,
        string id,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(id))
        {
            return TypedResults.BadRequest("id is required");
        }

        if (!context.ItemTemplateService!.TryGet(id, out var template) || template is null)
        {
            return TypedResults.NotFound();
        }

        return Results.Json(
            MapItemTemplateToHttpDetail(context, template),
            MoongateHttpJsonContext.Default.MoongateHttpItemTemplateDetail
        );
    }

    private static IResult HandleGetItemTemplateImageByItemId(
        MoongateHttpRouteContext context,
        string itemIdText,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (context.ArtService is null)
        {
            return TypedResults.NotFound();
        }

        if (!TryParseHexItemId(itemIdText, out var itemId))
        {
            return TypedResults.BadRequest("itemId must be in 0x... format");
        }

        var itemsImageDirectory = Path.Combine(context.DirectoriesConfig[DirectoryType.Images], "items");
        Directory.CreateDirectory(itemsImageDirectory);

        var normalizedHex = itemId.ToString("X4", CultureInfo.InvariantCulture);
        var cachePath = Path.Combine(itemsImageDirectory, $"0x{normalizedHex}.png");

        if (File.Exists(cachePath))
        {
            return Results.File(cachePath, "image/png");
        }

        var legacyPath = Directory.EnumerateFiles(itemsImageDirectory, $"*_{normalizedHex}.png")
                                  .FirstOrDefault();

        if (legacyPath is not null)
        {
            return Results.File(legacyPath, "image/png");
        }

        using var image = context.ArtService.GetArt(itemId);

        if (image is null)
        {
            return TypedResults.NotFound();
        }

        using (var stream = File.Create(cachePath))
        {
            image.Save(stream, new PngEncoder());
        }

        return Results.File(cachePath, "image/png");
    }

    private static IResult HandleGetItemTemplates(
        MoongateHttpRouteContext context,
        int page,
        int pageSize,
        string? name,
        string? tag,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
        IEnumerable<ItemTemplateDefinition> templates = context.ItemTemplateService!.GetAll();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameTerm = name.Trim();
            templates = templates.Where(
                template => !string.IsNullOrWhiteSpace(template.Name) &&
                            template.Name.Contains(nameTerm, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagTerm = tag.Trim();
            templates = templates.Where(
                template => template.Tags.Any(
                    existingTag => !string.IsNullOrWhiteSpace(existingTag) &&
                                   existingTag.Contains(tagTerm, StringComparison.OrdinalIgnoreCase)
                )
            );
        }

        var filteredTemplates = templates.ToList();
        var totalCount = filteredTemplates.Count;
        var skip = (safePage - 1) * safePageSize;
        var items = filteredTemplates.Skip(skip)
                                     .Take(safePageSize)
                                     .Select(MapItemTemplateToHttpSummary)
                                     .ToList();

        var response = new MoongateHttpItemTemplatePage
        {
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            Items = items
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpItemTemplatePage);
    }

    private static IResult HandleGetMapImage(
        MoongateHttpRouteContext context,
        int mapId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var mapsImageDirectory = Path.Combine(context.DirectoriesConfig[DirectoryType.Images], "maps");
        Directory.CreateDirectory(mapsImageDirectory);

        var cachePath = Path.Combine(mapsImageDirectory, $"{mapId}.png");

        if (File.Exists(cachePath))
        {
            return Results.File(cachePath, "image/png");
        }

        using var image = context.MapImageService!.GetMapImage(mapId);

        if (image is null)
        {
            return TypedResults.NotFound();
        }

        using (var stream = File.Create(cachePath))
        {
            image.Save(stream, new PngEncoder());
        }

        return Results.File(cachePath, "image/png");
    }

    private static IResult HandleGetUserById(
        MoongateHttpRouteContext context,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        var parsedId = ParseAccountIdOrNull(accountId);

        if (!parsedId.HasValue)
        {
            return TypedResults.BadRequest("invalid accountId");
        }

        var account = context.AccountService!
                             .GetAccountAsync(parsedId.Value)
                             .GetAwaiter()
                             .GetResult();

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapAccountToHttpUser(account));
    }

    private static IResult HandleGetUsers(
        MoongateHttpRouteContext context,
        CancellationToken cancellationToken
    )
    {
        var accounts = context.AccountService!
                              .GetAccountsAsync(cancellationToken)
                              .GetAwaiter()
                              .GetResult();
        var users = accounts.Select(MapAccountToHttpUser).ToList();

        return TypedResults.Ok((IReadOnlyList<MoongateHttpUser>)users);
    }

    private static IResult HandleHealth(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return TypedResults.Text("ok");
    }

    private static IResult HandleGetPortalMe(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var accountIdClaim = user.FindFirst("account_id")?.Value;
        var accountId = string.IsNullOrWhiteSpace(accountIdClaim) ? null : ParseAccountIdOrNull(accountIdClaim);

        if (accountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var account = context.AccountService!.GetAccountAsync(accountId.Value).GetAwaiter().GetResult();

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var characters = context.CharacterService!
                                .GetCharactersForAccountAsync(accountId.Value)
                                .GetAwaiter()
                                .GetResult();

        var response = new MoongateHttpPortalAccount
        {
            AccountId = account.Id.Value.ToString(),
            Username = account.Username,
            Email = account.Email,
            AccountType = account.AccountType.ToString(),
            Characters = characters.Select(
                                     static character => new MoongateHttpPortalCharacter
                                     {
                                         CharacterId = character.Id.Value.ToString(),
                                         Name = character.Name,
                                         MapId = character.MapId,
                                         X = character.Location.X,
                                         Y = character.Location.Y
                                     }
                                 )
                                 .ToList()
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpPortalAccount);
    }

    private static IResult HandleLogin(
        MoongateHttpRouteContext context,
        MoongateHttpLoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.Text("username and password are required", statusCode: StatusCodes.Status400BadRequest);
        }

        var account = context.AccountService!
                             .LoginAsync(request.Username, request.Password)
                             .GetAwaiter()
                             .GetResult();

        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var user = new MoongateHttpAuthenticatedUser
        {
            AccountId = account.Id.Value.ToString(),
            Username = account.Username,
            Role = account.AccountType.ToString()
        };

        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(context.JwtOptions.ExpirationMinutes);
        var token = CreateJwtToken(user, expiresAtUtc, context.JwtOptions);

        var response = new MoongateHttpLoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            AccountId = user.AccountId,
            Username = user.Username,
            Role = user.Role
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpLoginResponse);
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

    private static IResult HandleUpdateUser(
        MoongateHttpRouteContext context,
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var parsedId = ParseAccountIdOrNull(accountId);

        if (!parsedId.HasValue)
        {
            return TypedResults.BadRequest("invalid accountId");
        }

        if (
            request.Username is null &&
            request.Password is null &&
            request.Email is null &&
            request.Role is null &&
            request.IsLocked is null
        )
        {
            return TypedResults.BadRequest("at least one field must be provided");
        }

        AccountType? role = null;

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<AccountType>(request.Role, true, out var parsedRole))
            {
                return TypedResults.BadRequest("invalid role");
            }

            role = parsedRole;
        }

        var updated = context.AccountService!
                             .UpdateAccountAsync(
                                 parsedId.Value,
                                 request.Username,
                                 request.Password,
                                 request.Email,
                                 role,
                                 request.IsLocked,
                                 cancellationToken
                             )
                             .GetAwaiter()
                             .GetResult();

        return updated is null
                   ? TypedResults.NotFound()
                   : TypedResults.Ok(MapAccountToHttpUser(updated));
    }

    private static MoongateHttpUser MapAccountToHttpUser(UOAccountEntity account)
        => new()
        {
            AccountId = account.Id.Value.ToString(),
            Username = account.Username,
            Email = account.Email,
            Role = account.AccountType.ToString(),
            IsLocked = account.IsLocked,
            CreatedUtc = account.CreatedUtc,
            LastLoginUtc = account.LastLoginUtc,
            CharacterCount = account.CharacterIds.Count
        };

    private static MoongateHttpItemTemplateSummary MapItemTemplateToHttpSummary(ItemTemplateDefinition template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Category = template.Category,
            ItemId = template.ItemId,
            Params = template.Params.ToDictionary(
                static kvp => kvp.Key,
                static kvp => new ItemTemplateParamDefinition
                {
                    Type = kvp.Value.Type,
                    Value = kvp.Value.Value
                },
                StringComparer.OrdinalIgnoreCase
            )
        };

    private static MoongateHttpItemTemplateDetail MapItemTemplateToHttpDetail(
        MoongateHttpRouteContext context,
        ItemTemplateDefinition template
    )
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Category = template.Category,
            ItemId = template.ItemId,
            Description = template.Description,
            Tags = template.Tags.ToList(),
            ScriptId = template.ScriptId,
            Weight = template.Weight > 0 ? template.Weight : null,
            GoldValue = template.GoldValue.ToString(),
            Hue = template.Hue.ToString(),
            GumpId = template.GumpId,
            Rarity = template.Rarity.ToString(),
            Container = template.Container.ToList(),
            ContainerItems = template.Container
                                     .Select(
                                         containerId =>
                                         {
                                             if (!context.ItemTemplateService!.TryGet(containerId, out var childTemplate) ||
                                                 childTemplate is null)
                                             {
                                                 return null;
                                             }

                                             return new MoongateHttpItemTemplateContainerItem
                                             {
                                                 Id = childTemplate.Id,
                                                 Name = childTemplate.Name,
                                                 ItemId = childTemplate.ItemId
                                             };
                                         }
                                     )
                                     .OfType<MoongateHttpItemTemplateContainerItem>()
                                     .ToList(),
            Params = template.Params.ToDictionary(
                static kvp => kvp.Key,
                static kvp => new ItemTemplateParamDefinition
                {
                    Type = kvp.Value.Type,
                    Value = kvp.Value.Value
                },
                StringComparer.OrdinalIgnoreCase
            )
        };

    private static Serial? ParseAccountIdOrNull(string accountId)
        => uint.TryParse(accountId, out var parsedId) ? (Serial)parsedId : null;

    private static bool TryParseHexItemId(string itemIdText, out int itemId)
    {
        itemId = 0;

        if (string.IsNullOrWhiteSpace(itemIdText))
        {
            return false;
        }

        var value = itemIdText.Trim();

        if (!value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(
            value.AsSpan(2),
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out itemId
        );
    }
}
