using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Accounts;

namespace Moongate.Http.Plugin.Services.Characters;

/// <summary>
/// Answers one question for every route about a named character: may this caller read it?
///
/// It lives in one place because two copies of an authorization rule are two rules, and the second
/// one drifts. Every route that addresses a character by serial resolves it through here.
/// </summary>
public sealed class CharacterAccessService
{
    private readonly IAccountService _accounts;
    private readonly ICharacterQueryService _characters;

    public CharacterAccessService(IAccountService accounts, ICharacterQueryService characters)
    {
        _accounts = accounts;
        _characters = characters;
    }

    /// <summary>
    /// The character the caller may read, or the response saying why they may not. Exactly one of the
    /// two is non-null.
    /// </summary>
    /// <remarks>
    /// An account reads its own characters and staff read anyone's. Ownership is checked against the
    /// account the token names, never against a value the caller supplied: an id from the request
    /// would let anyone read anyone's character by changing a number.
    /// </remarks>
    public (OwnedCharacter? Character, IResult? Denial) Resolve(string serial, ClaimsPrincipal user)
    {
        if (!Serial.TryParse(serial, out var characterId))
        {
            return (null, NotFound());
        }

        var found = _characters.Find(characterId);

        if (found is null)
        {
            return (null, NotFound());
        }

        if (!CharacterEndpoints.TryReadAccountId(user, out var accountId))
        {
            return (null, Results.Problem(
                        "The token carries no account id.",
                        statusCode: StatusCodes.Status401Unauthorized
                    ));
        }

        var caller = _accounts.GetById(accountId);

        if (caller is null)
        {
            return (null, Results.Problem(
                        "The account this token belongs to no longer exists.",
                        statusCode: StatusCodes.Status401Unauthorized
                    ));
        }

        if (!IsStaff(caller.AccountLevel) && !caller.MobileIds.Contains(characterId))
        {
            return (null, Results.Problem(
                        "That character belongs to another account.",
                        statusCode: StatusCodes.Status403Forbidden
                    ));
        }

        return (found, null);
    }

    /// <summary>
    /// One answer for "no such character" and "that is not even a serial": from outside, both mean
    /// there is nothing at that address.
    /// </summary>
    public static IResult NotFound()
        => Results.Problem("No character with that serial.", statusCode: StatusCodes.Status404NotFound);

    /// <summary>The same two levels the admin policy admits, so the two cannot drift apart.</summary>
    private static bool IsStaff(AccountLevelType level)
        => level is AccountLevelType.Administrator or AccountLevelType.GrandMaster;
}
