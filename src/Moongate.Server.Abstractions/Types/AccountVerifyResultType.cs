namespace Moongate.Server.Abstractions.Types;

/// <summary>The outcome of consuming an email-verification token.</summary>
public enum AccountVerifyResultType
{
    Verified,
    InvalidToken
}
