namespace Moongate.Network.Packets.Types.Login;

/// <summary>
///     Reason codes carried by the login denied packet (0x82).
/// </summary>
public enum LoginDeniedReason : byte
{
    IncorrectNameOrPassword = 0x00,
    AccountAlreadyInUse = 0x01,
    AccountBlocked = 0x02,
    InvalidCredentials = 0x03,
    CommunicationProblem = 0x04,
    IgrConcurrencyLimitMet = 0x05,
    IgrTimeLimitMet = 0x06,
    GeneralIgrAuthenticationFailure = 0x07
}
