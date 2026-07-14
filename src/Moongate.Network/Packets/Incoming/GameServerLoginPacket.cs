using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Game server login (0x91): the auth key from the redirect plus the account credentials.</summary>
public readonly record struct GameServerLoginPacket(uint AuthKey, string Account, string Password)
    : IIncomingPacket<GameServerLoginPacket>
{
    public static byte PacketId => 0x91;

    public static GameServerLoginPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var authKey = reader.ReadUInt32();
        var account = reader.ReadAscii(30);
        var password = reader.ReadAscii(30);

        return new(authKey, account, password);
    }
}
