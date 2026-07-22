using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;

namespace Moongate.Http.Plugin.Interfaces.Auth;

/// <summary>Mints the bearer tokens the REST API authenticates with.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Issues a token for an account that has already been authenticated. The account level rides in the
    /// role claim, which is what the <c>admin</c> and <c>player</c> policies read.
    /// <para>
    /// <paramref name="authTime" /> marks when the session began, not when this token was minted: a renewal
    /// passes on the value it was given, so the chain of renewals stays bounded by
    /// <c>http.Jwt.MaxSessionHours</c>.
    /// </para>
    /// </summary>
    ApiTokenResult Issue(Serial accountId, string username, AccountLevelType level, DateTimeOffset authTime);
}
