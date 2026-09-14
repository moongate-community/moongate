using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class GameLoginPacket : IIncomingPacket<GameLoginPacket>
{
    private const byte PacketOpCode = 0x91;
    private const int PacketLength = 65;
    private const int CredentialLength = 30;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
    public uint AuthKey { get; }
    public string Account { get; }
    public string Password { get; }

    public GameLoginPacket(uint authKey, string account, string password)
    {
        PacketValidation.ValidateFixedAscii(account, CredentialLength, nameof(account));
        PacketValidation.ValidateFixedAscii(password, CredentialLength, nameof(password));
        AuthKey = authKey;
        Account = account;
        Password = password;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out GameLoginPacket? packet)
    {
        packet = null;
        if (!PacketValidation.HasFixedHeader(data, PacketOpCode, PacketLength))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt32BigEndian(out var authKey)
            || !reader.TryReadFixedAscii(CredentialLength, out var account)
            || !reader.TryReadFixedAscii(CredentialLength, out var password))
        {
            return false;
        }

        packet = new GameLoginPacket(authKey, account!, password!);
        return true;
    }
}
