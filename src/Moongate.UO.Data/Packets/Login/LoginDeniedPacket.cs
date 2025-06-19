using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Login;

public class LoginDeniedPacket : BaseUoPacket
{
    public byte Reason { get; set; }

    public LoginDeniedPacket(LoginDeniedReason reason) : base(0x82)
    {
        Reason = (byte)reason;
    }

    public LoginDeniedPacket() : base(0x82)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Reason);
        return writer.ToArray();
    }
}

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
