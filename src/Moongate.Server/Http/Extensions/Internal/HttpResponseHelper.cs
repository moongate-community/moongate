using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Moongate.Server.Http.Extensions.Internal;

internal static class HttpResponseHelper
{
    public static IResult ForbidOrUnauthorized(ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true ? TypedResults.Forbid() : TypedResults.Unauthorized();
}
