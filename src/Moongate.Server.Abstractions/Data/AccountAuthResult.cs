using Moongate.Network.Types;

namespace Moongate.Server.Abstractions.Data;

/// <summary>Outcome of an account authentication attempt.</summary>
public readonly record struct AccountAuthResult(bool Success, string Username, LoginDeniedReasonType Reason)
{
    public static AccountAuthResult Denied(LoginDeniedReasonType reason)
        => new(false, string.Empty, reason);

    public static AccountAuthResult Ok(string username)
        => new(true, username, LoginDeniedReasonType.IncorrectCredentials);
}
