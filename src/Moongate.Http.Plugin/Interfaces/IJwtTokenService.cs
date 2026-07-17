using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;

namespace Moongate.Http.Plugin.Interfaces;

/// <summary>Mints the bearer tokens the REST API authenticates with.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Issues a token for an account that has already been authenticated. The account level rides in the
    /// role claim, which is what the <c>admin</c> and <c>player</c> policies read.
    /// </summary>
    ApiTokenResult Issue(Serial accountId, string username, AccountLevelType level);
}
