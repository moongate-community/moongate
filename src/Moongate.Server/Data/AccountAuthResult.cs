using Moongate.Network.Types;

namespace Moongate.Server.Data;

/// <summary>Outcome of an account authentication attempt.</summary>
public readonly record struct AccountAuthResult(bool Success, string Username, LoginDeniedReasonType Reason)
{
    public static AccountAuthResult Ok(string username)
    {
        return new AccountAuthResult(true, username, LoginDeniedReasonType.IncorrectCredentials);
    }

    public static AccountAuthResult Denied(LoginDeniedReasonType reason)
    {
        return new AccountAuthResult(false, string.Empty, reason);
    }
}
