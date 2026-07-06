using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Account login request (0x80): credentials for the login server.</summary>
public readonly record struct AccountLoginRequestPacket(string Account, string Password, byte NextLoginKey)
{
    public const byte PacketId = 0x80;

    public static AccountLoginRequestPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var account = reader.ReadAscii(30);
        var password = reader.ReadAscii(30);
        var nextLoginKey = reader.ReadByte();

        return new AccountLoginRequestPacket(account, password, nextLoginKey);
    }
}
