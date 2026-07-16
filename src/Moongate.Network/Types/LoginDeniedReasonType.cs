namespace Moongate.Network.Types;

/// <summary>Reason codes carried by the login denied packet (0x82).</summary>
public enum LoginDeniedReasonType : byte
{
    IncorrectCredentials = 0x00,
    AccountInUse = 0x01,
    AccountBlocked = 0x02,
    BadCredentials = 0x03,
    CommunicationProblem = 0x04,
    IgrConcurrencyLimit = 0x05,
    IgrTimeLimit = 0x06,
    IgrAuthenticationFailure = 0x07
}
