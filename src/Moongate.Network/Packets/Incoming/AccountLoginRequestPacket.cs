using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Account login request (0x80): credentials for the login server.</summary>
[PacketDocumentation(PacketFamilyType.LoginShardSelect)]
public readonly record struct AccountLoginRequestPacket(string Account, string Password, byte NextLoginKey)
    : IIncomingPacket<AccountLoginRequestPacket>
{
    public static byte PacketId => 0x80;

    public static AccountLoginRequestPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var account = reader.ReadAscii(30);
        var password = reader.ReadAscii(30);
        var nextLoginKey = reader.ReadByte();

        return new(account, password, nextLoginKey);
    }
}
