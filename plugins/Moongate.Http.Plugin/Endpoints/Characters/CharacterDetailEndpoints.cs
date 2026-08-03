using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Characters;
using Moongate.Http.Plugin.Services.Hosting;

namespace Moongate.Http.Plugin.Endpoints.Characters;

/// <summary>One character in full, for its owner or for staff.</summary>
public sealed class CharacterDetailEndpoints : IApiEndpointRegistration
{
    private readonly CharacterAccessService _access;
    private readonly CharacterInventoryReader _inventory;
    private readonly CharacterSkillReader _skills;

    public CharacterDetailEndpoints(
        CharacterAccessService access,
        CharacterInventoryReader inventory,
        CharacterSkillReader skills
    )
    {
        _access = access;
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
    /// - The backpack is a tree: nested containers are expanded, to a depth of 10. Equipment lists
    /// the worn items by layer, the backpack and the bank box among them, without expanding them.
    /// - Skills are the ones the character has, by name, in points rather than the tenths the entity
    /// stores.
    /// - Read off the game loop, so an item the loop moves mid-read may appear in neither place or
    /// in both. The next request settles it: this is a view, not a ledger.
    /// </remarks>
    private IResult GetOne(string serial, ClaimsPrincipal user)
    {
        // Who may read which character is one rule, and it lives in one place: two copies of an
        // authorization check are two rules, and the second one drifts.
        var (found, denial) = _access.Resolve(serial, user);

        if (denial is not null)
        {
            return denial;
        }

        return Results.Ok(
            new CharacterDetailResponse(
                CharacterResponse.From(found!.Mobile, found.AccountUsername),
                _inventory.ReadEquipment(found.Mobile),
                _inventory.ReadBackpack(found.Mobile),
                _skills.Read(found.Mobile)
            )
        );
    }
}
