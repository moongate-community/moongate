using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Utils;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Extensions.Internal;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Http.Extensions;

internal static class PortalRouteExtensions
{
    public static IEndpointRouteBuilder MapPortalRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.AccountService is null || context.CharacterService is null)
        {
            return endpoints;
        }

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

        portalGroup.MapGet(
                       "/characters/{characterId}/inventory",
                       (ClaimsPrincipal user, string characterId, CancellationToken cancellationToken) =>
                           HandleGetPortalCharacterInventory(context, user, characterId, cancellationToken)
                   )
                   .WithName("PortalGetCharacterInventory")
                   .WithSummary("Returns the authenticated player's inventory table for one character.")
                   .Produces<MoongateHttpPortalInventory>()
                   .Produces(StatusCodes.Status401Unauthorized)
                   .Produces(StatusCodes.Status404NotFound);

        portalGroup.MapPut(
                       "/me",
                       (
                           ClaimsPrincipal user,
                           MoongateHttpUpdatePortalProfileRequest request,
                           CancellationToken cancellationToken
                       ) => HandleUpdatePortalMe(context, user, request, cancellationToken)
                   )
                   .WithName("PortalUpdateMe")
                   .WithSummary("Updates the authenticated player's editable profile fields.")
                   .Accepts<MoongateHttpUpdatePortalProfileRequest>("application/json")
                   .Produces<MoongateHttpPortalAccount>()
                   .Produces(StatusCodes.Status400BadRequest)
                   .Produces(StatusCodes.Status401Unauthorized)
                   .Produces(StatusCodes.Status404NotFound);

        portalGroup.MapPut(
                       "/me/password",
                       (
                           ClaimsPrincipal user,
                           MoongateHttpUpdatePortalPasswordRequest request,
                           CancellationToken cancellationToken
                       ) => HandleUpdatePortalPassword(context, user, request, cancellationToken)
                   )
                   .WithName("PortalUpdateMyPassword")
                   .WithSummary("Updates the authenticated player's password.")
                   .Accepts<MoongateHttpUpdatePortalPasswordRequest>("application/json")
                   .Produces(StatusCodes.Status200OK)
                   .Produces(StatusCodes.Status400BadRequest)
                   .Produces(StatusCodes.Status401Unauthorized)
                   .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static UOItemEntity? FindContainer(UOItemEntity root, Serial containerId)
    {
        if (root.Id == containerId)
        {
            return root;
        }

        foreach (var item in root.Items)
        {
            if (item.Id == containerId)
            {
                return item;
            }

            if (item.Items.Count == 0)
            {
                continue;
            }

            var nested = FindContainer(item, containerId);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<UOItemEntity> FlattenContainerItems(UOItemEntity container)
    {
        foreach (var item in container.Items.OrderBy(static child => child.Name ?? string.Empty, StringComparer.Ordinal))
        {
            yield return item;

            if (item.Items.Count == 0)
            {
                continue;
            }

            foreach (var descendant in FlattenContainerItems(item))
            {
                yield return descendant;
            }
        }
    }

    private static IResult HandleGetPortalCharacterInventory(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string characterId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var accountId = HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user);

        if (accountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var parsedCharacterId = HttpRouteAccessHelper.ParseAccountIdOrNull(characterId);

        if (parsedCharacterId is null)
        {
            return TypedResults.NotFound();
        }

        var character = context.CharacterService!.GetCharacterAsync(parsedCharacterId.Value).GetAwaiter().GetResult();

        if (character is null || character.AccountId != accountId.Value)
        {
            return TypedResults.NotFound();
        }

        var backpack = context.CharacterService.GetBackpackWithItemsAsync(character).GetAwaiter().GetResult();
        var bankBox = context.CharacterService.GetBankBoxWithItemsAsync(character).GetAwaiter().GetResult();
        var items = MapPortalInventoryItems(character, backpack);
        var bankItems = MapPortalContainerItems(bankBox, "Bank");
        var response = new MoongateHttpPortalInventory
        {
            CharacterId = character.Id.Value.ToString(),
            CharacterName = character.Name ?? character.Id.Value.ToString(),
            Items = items,
            BankItems = bankItems
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpPortalInventory);
    }

    private static IResult HandleGetPortalMe(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var accountId = HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user);

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

        return Results.Json(
            MapPortalAccount(account, characters),
            MoongateHttpJsonContext.Default.MoongateHttpPortalAccount
        );
    }

    private static IResult HandleUpdatePortalMe(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        MoongateHttpUpdatePortalProfileRequest request,
        CancellationToken cancellationToken
    )
    {
        var accountId = HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user);

        if (accountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email) || !HttpRouteAccessHelper.IsValidEmail(email))
        {
            return TypedResults.BadRequest("invalid email");
        }

        var updated = context.AccountService!
                             .UpdateAccountAsync(accountId.Value, email: email, cancellationToken: cancellationToken)
                             .GetAwaiter()
                             .GetResult();

        if (updated is null)
        {
            return TypedResults.NotFound();
        }

        var characters = context.CharacterService!
                                .GetCharactersForAccountAsync(accountId.Value)
                                .GetAwaiter()
                                .GetResult();

        return Results.Json(
            MapPortalAccount(updated, characters),
            MoongateHttpJsonContext.Default.MoongateHttpPortalAccount
        );
    }

    private static IResult HandleUpdatePortalPassword(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        MoongateHttpUpdatePortalPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var accountId = HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user);

        if (accountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var account = context.AccountService!.GetAccountAsync(accountId.Value).GetAwaiter().GetResult();

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var newPassword = request.NewPassword?.Trim();
        var confirmPassword = request.ConfirmPassword?.Trim();

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return TypedResults.BadRequest("new password is required");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest("confirm password does not match");
        }

        if (account.AccountType == AccountType.Regular)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                !HashUtils.VerifyPassword(request.CurrentPassword, account.PasswordHash))
            {
                return TypedResults.BadRequest("current password is invalid");
            }
        }

        var updated = context.AccountService!
                             .UpdateAccountAsync(
                                 accountId.Value,
                                 password: newPassword,
                                 clearRecoveryCode: true,
                                 cancellationToken: cancellationToken
                             )
                             .GetAwaiter()
                             .GetResult();

        if (updated is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    private static MoongateHttpPortalAccount MapPortalAccount(
        UOAccountEntity account,
        IReadOnlyList<UOMobileEntity> characters
    )
        => new()
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
                                           MapName = ResolveMapName(character.MapId),
                                           X = character.Location.X,
                                           Y = character.Location.Y
                                       }
                                   )
                                   .ToList()
        };

    private static IReadOnlyList<MoongateHttpPortalInventoryItem> MapPortalContainerItems(
        UOItemEntity? rootContainer,
        string rootLabel
    )
    {
        if (rootContainer is null)
        {
            return [];
        }

        var items = new List<MoongateHttpPortalInventoryItem>();

        foreach (var item in FlattenContainerItems(rootContainer))
        {
            var container = item.ParentContainerId == rootContainer.Id
                                ? null
                                : FindContainer(rootContainer, item.ParentContainerId);
            var location = item.ParentContainerId == rootContainer.Id
                               ? rootLabel
                               : $"Container: {container?.Name ?? item.ParentContainerId.Value.ToString()}";

            items.Add(MapPortalInventoryItem(item, location, null, container));
        }

        return items;
    }

    private static MoongateHttpPortalInventoryItem MapPortalInventoryItem(
        UOItemEntity item,
        string location,
        string? layer,
        UOItemEntity? container
    )
        => new()
        {
            ItemId = $"0x{item.ItemId:X4}",
            Serial = item.Id.Value.ToString(),
            Name = item.Name ?? $"0x{item.ItemId:X4}",
            Graphic = item.ItemId,
            Hue = item.Hue,
            Amount = item.Amount,
            Location = location,
            Layer = layer,
            ContainerSerial = container is null ? null : container.Id.Value.ToString(),
            ContainerName = container?.Name,
            ImageUrl = $"/api/item-templates/by-item-id/0x{item.ItemId:X4}/image"
        };

    private static IReadOnlyList<MoongateHttpPortalInventoryItem> MapPortalInventoryItems(
        UOMobileEntity character,
        UOItemEntity? backpack
    )
    {
        var items = new List<MoongateHttpPortalInventoryItem>();

        foreach (var item in character.GetEquippedItemsRuntime()
                                      .Where(static equipped => equipped.EquippedLayer != ItemLayerType.Bank)
                                      .OrderBy(
                                          static equipped => equipped.EquippedLayer?.ToString(),
                                          StringComparer.Ordinal
                                      ))
        {
            var layerLabel = item.EquippedLayer is null ? null : ResolveLayerLabel(item.EquippedLayer.Value);
            items.Add(MapPortalInventoryItem(item, $"Equipped: {layerLabel}", layerLabel, null));
        }

        items.AddRange(MapPortalContainerItems(backpack, "Backpack"));

        return items;
    }

    private static string ResolveLayerLabel(ItemLayerType layer)
        => layer switch
        {
            ItemLayerType.OneHanded or ItemLayerType.FirstValid    => "OneHanded",
            ItemLayerType.TwoHanded                                => "TwoHanded",
            ItemLayerType.InnerTorso                               => "InnerTorso",
            ItemLayerType.MiddleTorso                              => "MiddleTorso",
            ItemLayerType.OuterTorso                               => "OuterTorso",
            ItemLayerType.OuterLegs                                => "OuterLegs",
            ItemLayerType.InnerLegs or ItemLayerType.LastUserValid => "InnerLegs",
            ItemLayerType.LastValid                                => "Bank",
            _                                                      => layer.ToString()
        };

    private static string ResolveMapName(int mapId)
        => mapId switch
        {
            0 => "Felucca",
            1 => "Trammel",
            2 => "Ilshenar",
            3 => "Malas",
            4 => "Tokuno",
            5 => "TerMur",
            _ => $"Map {mapId}"
        };
}
