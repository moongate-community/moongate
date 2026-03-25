using System.Net.Mail;
using System.Security.Claims;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Http.Extensions.Internal;

internal static class HttpRouteAccessHelper
{
    public static bool IsAdministrativeUser(ClaimsPrincipal user)
        => user.IsInRole(AccountType.Administrator.ToString()) ||
           user.IsInRole(AccountType.GameMaster.ToString());

    public static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Serial? ParseAccountIdOrNull(string accountId)
        => uint.TryParse(accountId, out var parsedId) ? (Serial)parsedId : null;

    public static Serial? ResolveAuthenticatedAccountId(ClaimsPrincipal user)
    {
        var accountIdClaim = user.FindFirst("account_id")?.Value;

        return string.IsNullOrWhiteSpace(accountIdClaim) ? null : ParseAccountIdOrNull(accountIdClaim);
    }
}
