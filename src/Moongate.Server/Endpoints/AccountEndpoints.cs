using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Api;
using Moongate.Server.Interfaces.Accounts;

namespace Moongate.Server.Endpoints;

/// <summary>Account administration: the REST twin of Lua's <c>account.*</c> module.</summary>
public sealed class AccountEndpoints : IApiEndpointRegistration
{
    private readonly IAccountService _accounts;

    public AccountEndpoints(IAccountService accounts)
    {
        _accounts = accounts;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/accounts")
                          .WithTags("accounts")
                          .RequireAuthorization(HttpServerService.AdminPolicy);

        group.MapGet("/", List).WithName("ListAccounts");
        group.MapGet("/{username}", Get).WithName("GetAccount");
    }

    private IResult List()
        => Results.Ok(_accounts.GetAll().Select(ToResponse));

    private IResult Get(string username)
        => _accounts.GetByUsername(username) is { } account
               ? Results.Ok(ToResponse(account))
               : NotFound(username);

    /// <summary>
    /// CharacterCount comes from the entity's own id list, which is free. It must not come from
    /// <c>ICharacterService.GetPlayerCharacters</c>, which scans the account store and the mobile store
    /// on every call — quadratic over a list, and pointless when the entity already holds the ids.
    /// </summary>
    internal static AccountResponse ToResponse(AccountEntity account)
        => new(
            account.Username,
            account.Email,
            account.AccountLevel.ToString(),
            account.IsActive,
            account.MobileIds.Count
        );

    /// <summary>
    /// Says which account was not found. Unlike login's flat 401, these routes name the reason: the
    /// caller already holds a staff token, so it is operational information for someone entitled to it
    /// rather than an oracle telling an attacker which usernames exist.
    /// </summary>
    internal static IResult NotFound(string username)
        => Results.Problem($"No account named '{username}'.", statusCode: StatusCodes.Status404NotFound);
}
