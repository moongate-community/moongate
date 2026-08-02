using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Characters;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Interfaces.Accounts;

namespace Moongate.Http.Plugin.Endpoints.Characters;

/// <summary>One character in full, for its owner or for staff.</summary>
public sealed class CharacterDetailEndpoints : IApiEndpointRegistration
{
    private readonly IAccountService _accounts;
    private readonly ICharacterQueryService _characters;
    private readonly CharacterInventoryReader _inventory;
    private readonly CharacterSkillReader _skills;

    public CharacterDetailEndpoints(
        IAccountService accounts,
        ICharacterQueryService characters,
        CharacterInventoryReader inventory,
        CharacterSkillReader skills
    )
    {
        _accounts = accounts;
        _characters = characters;
        _inventory = inventory;
        _skills = skills;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/characters/{serial}", GetOne)
                 .WithName("GetCharacter")
                 .WithTags("characters")
                 .Produces<CharacterDetailResponse>()
                 .RequireAuthorization(HttpServerService.PlayerPolicy);

    /// <summary>One character in full: stats, appearance, what they wear and what they carry.</summary>
    /// <remarks>
    /// The serial takes the form the rest of the API reports, <c>0x40000001</c>, or plain decimal. An
    /// account may read its own characters; staff may read anyone's, and everyone else gets 403. A
    /// serial naming no character is 404, and so is one that is not a number.
    ///
    /// The backpack is a tree: nested containers are expanded, to a depth of 10. Equipment lists the
    /// worn items by layer, the backpack and the bank box among them, without expanding them. Skills
    /// are the ones the character has, by name, in points rather than the tenths the entity stores.
    ///
    /// Read off the game loop, so an item the loop moves mid-read may appear in neither place or in
    /// both. The next request settles it: this is a view, not a ledger.
    /// </remarks>
    private IResult GetOne(string serial, ClaimsPrincipal user)
    {
        if (!Serial.TryParse(serial, out var characterId))
        {
            return NotFound();
        }

        var found = _characters.Find(characterId);

        if (found is null)
        {
            return NotFound();
        }

        // The account comes from the token, never from the request: an id a caller could supply would
        // let anyone read anyone's character by changing a number.
        if (!CharacterEndpoints.TryReadAccountId(user, out var accountId))
        {
            return Results.Problem("The token carries no account id.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var caller = _accounts.GetById(accountId);

        if (caller is null)
        {
            return Results.Problem(
                "The account this token belongs to no longer exists.",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        if (!IsStaff(caller.AccountLevel) && !caller.MobileIds.Contains(characterId))
        {
            return Results.Problem(
                "That character belongs to another account.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        return Results.Ok(
            new CharacterDetailResponse(
                CharacterResponse.From(found.Mobile, found.AccountUsername),
                _inventory.ReadEquipment(found.Mobile),
                _inventory.ReadBackpack(found.Mobile),
                _skills.Read(found.Mobile)
            )
        );
    }

    /// <summary>The same two levels the admin policy admits, so the two cannot drift apart.</summary>
    private static bool IsStaff(AccountLevelType level)
        => level is AccountLevelType.Administrator or AccountLevelType.GrandMaster;

    /// <summary>
    /// One answer for "no such character" and "that is not even a serial": from outside, both mean
    /// there is nothing at that address.
    /// </summary>
    private static IResult NotFound()
        => Results.Problem("No character with that serial.", statusCode: StatusCodes.Status404NotFound);
}
