using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class AccountLoginPacket : IIncomingPacket<AccountLoginPacket>
{
    private const byte PacketOpCode = 0x80;
    private const int PacketLength = 62;
    private const int CredentialLength = 30;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
    public string Account { get; }
    public string Password { get; }
    public byte NextLoginKey { get; }

    public AccountLoginPacket(string account, string password, byte nextLoginKey)
    {
        PacketValidation.ValidateFixedAscii(account, CredentialLength, nameof(account));
        PacketValidation.ValidateFixedAscii(password, CredentialLength, nameof(password));
        Account = account;
        Password = password;
        NextLoginKey = nextLoginKey;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out AccountLoginPacket? packet)
    {
        packet = null;
        if (!PacketValidation.HasFixedHeader(data, PacketOpCode, PacketLength))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadFixedAscii(CredentialLength, out var account)
            || !reader.TryReadFixedAscii(CredentialLength, out var password)
            || !reader.TryReadByte(out var nextLoginKey))
        {
            return false;
        }

        packet = new AccountLoginPacket(account!, password!, nextLoginKey);
        return true;
    }
}
