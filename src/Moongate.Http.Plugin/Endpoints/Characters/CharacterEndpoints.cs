using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.JsonWebTokens;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Server.Abstractions.Interfaces.Accounts;

namespace Moongate.Http.Plugin.Endpoints.Characters;

/// <summary>What an authenticated account may ask about its own characters.</summary>
public sealed class CharacterEndpoints : IApiEndpointRegistration
{
    private readonly IAccountService _accounts;
    private readonly ICharacterService _characters;

    public CharacterEndpoints(IAccountService accounts, ICharacterService characters)
    {
        _accounts = accounts;
        _characters = characters;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        // A method group, not a lambda: Swashbuckle reads the /// off the handler's method, and a lambda
        // has none — the route would document itself blank.
        routes.MapGet("/api/v1/player/me/characters", GetMine)
              .WithName("GetMyCharacters")
              .WithTags("player")
              .RequireAuthorization(HttpServerService.PlayerPolicy);
    }

    /// <summary>The caller's own characters.</summary>
    /// <remarks>
    /// Reports every character on the account the bearer token belongs to, with its stats, appearance and
    /// where it stands. Never another account's. The owner field is null here: the caller is the owner. An
    /// account with no characters gets an empty list, not an error.
    /// </remarks>
    private IResult GetMine(ClaimsPrincipal user)
    {
        // The account comes from the token, never from the query string: reading an id the caller supplied
        // would let anyone list anyone's characters by changing a number.
        if (!TryReadAccountId(user, out var accountId))
        {
            return Results.Problem(
                "The token carries no account id.",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        if (_accounts.GetById(accountId) is null)
        {
            return Results.Problem(
                "The account this token belongs to no longer exists.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Results.Ok(
            _characters.GetPlayerCharacters(accountId)
                       .Select(mobile => CharacterResponse.From(mobile, null))
        );
    }

    /// <summary>
    /// Reads the account id the token was issued with. JwtTokenService writes it into <c>sub</c>, but
    /// JwtBearer's inbound claim mapping is on by default and renames <c>sub</c> to NameIdentifier before
    /// the principal reaches here — so the mapped name is tried first and the raw one second, and the
    /// route survives that setting being changed either way.
    /// </summary>
    internal static bool TryReadAccountId(ClaimsPrincipal user, out Serial accountId)
    {
        accountId = default;

        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!uint.TryParse(sub, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        accountId = new(value);

        return true;
    }
}
